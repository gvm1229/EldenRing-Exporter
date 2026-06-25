import argparse
import os
import sys
import time
import traceback
from pathlib import Path

import bpy


class BatchLog:
    def info(self, msg):
        print(f"INFO {msg}", flush=True)

    def debug(self, msg):
        print(f"DEBUG {msg}", flush=True)

    def warning(self, msg):
        print(f"WARN {msg}", flush=True)

    def error(self, msg):
        print(f"ERROR {msg}", flush=True)
        return {"CANCELLED"}


def parse_args():
    parser = argparse.ArgumentParser(description="Export an Elden Ring character FLVER/HKX set to FBX or GLB.")
    parser.add_argument("--addon-root", required=True)
    parser.add_argument("--flver", required=True)
    parser.add_argument("--anibnd", required=True)
    parser.add_argument("--output")
    parser.add_argument("--fbx")
    parser.add_argument("--format", choices=("fbx", "glb"), default="fbx")
    parser.add_argument("--character", required=True)
    parser.add_argument("--anim", default="")
    parser.add_argument("--limit", type=int, default=0)
    parser.add_argument("--source-scale", type=float, default=100.0)
    parser.add_argument("--armature-object-name", default="root")
    parser.add_argument(
        "--apply-scale-options",
        choices=("FBX_SCALE_NONE", "FBX_SCALE_UNITS", "FBX_SCALE_CUSTOM", "FBX_SCALE_ALL"),
        default="FBX_SCALE_UNITS",
    )
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    if not args.output and not args.fbx:
        parser.error("--output is required")
    if args.fbx and not args.output:
        args.output = args.fbx
        args.format = "fbx"
    return args


def patch_background_gpu_imports():
    import gpu
    import gpu_extras.batch

    class DummyBatch:
        def draw(self, *args, **kwargs):
            pass

    gpu.shader.from_builtin = lambda *args, **kwargs: None
    gpu_extras.batch.batch_for_shader = lambda *args, **kwargs: DummyBatch()


def register_soulstruct(addon_root: Path):
    sys.path.insert(0, str(addon_root))
    patch_background_gpu_imports()
    import io_soulstruct

    io_soulstruct.register()
    bpy.context.scene.soulstruct_settings.game_enum = "ELDEN_RING"
    bpy.context.scene.animation_import_settings.to_60_fps = False
    if hasattr(bpy.context.scene, "flver_import_settings"):
        bpy.context.scene.flver_import_settings.import_textures = False


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for datablocks in (
        bpy.data.actions,
        bpy.data.armatures,
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.images,
    ):
        for datablock in list(datablocks):
            datablocks.remove(datablock, do_unlink=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0


def make_scaled_flver(source_flver: Path, factor: float) -> Path:
    if factor == 1.0:
        print("SCALED_FLVER_FACTOR 1", flush=True)
        return source_flver

    from soulstruct.flver import FLVER

    scaled_dir = source_flver.parent / f"_scaled_{factor:g}_{os.getpid()}"
    scaled_dir.mkdir(parents=True, exist_ok=True)
    scaled_flver = scaled_dir / source_flver.name

    flver = FLVER.from_path(source_flver)
    for dummy in flver.dummies:
        dummy.translate *= factor
    for bone in flver.bones:
        bone.translate *= factor
    flver.bounding_box_min *= factor
    flver.bounding_box_max *= factor
    for mesh in flver.meshes:
        for vertex_array in mesh.vertex_arrays:
            if "position" in vertex_array.array.dtype.names:
                vertex_array.array["position"] *= factor

    flver.write(scaled_flver)
    print(f"SCALED_FLVER_FACTOR {factor:g} path={scaled_flver}", flush=True)
    return scaled_flver


def import_flver(flver_path: Path):
    print("BLENDER_PROGRESS 1/5 importing FLVER skeletal mesh", flush=True)
    try:
        bpy.ops.import_scene.flver(
            directory=str(flver_path.parent) + "\\",
            files=[{"name": flver_path.name}],
        )
    except RuntimeError as ex:
        if "view3d.view_selected" not in str(ex):
            raise
        print("IGNORED background-only view_selected failure after successful FLVER import", flush=True)

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if not armatures:
        raise RuntimeError("FLVER import finished without creating an armature.")
    armature = armatures[0]
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    return armature


def normalize_armature_object_name(armature, object_name: str):
    armature.name = object_name
    armature.data.name = object_name


def import_animations(anibnd_path: Path, armature, character: str, anim_name: str, limit: int, source_scale: float):
    print("BLENDER_PROGRESS 2/5 loading ANIBND and importing HKX animations", flush=True)
    from soulstruct.blender.animation.types import SoulstructAnimation
    from soulstruct.blender.animation.utilities import read_animation_hkx_entry, read_skeleton_hkx_entry
    from soulstruct.eldenring.containers import DivBinder
    from soulstruct.havok.core import HKX

    anibnd = DivBinder.from_path(anibnd_path)
    compendium_entry = anibnd.find_entry_matching_name(r".*\.compendium")
    compendium = HKX.from_binder_entry(compendium_entry)
    skeleton_entry = anibnd.find_entry_matching_name(r"skeleton\.hkx(\.dcx)?")
    skeleton = read_skeleton_hkx_entry(skeleton_entry, compendium)
    if source_scale != 1.0:
        skeleton.skeleton.scale_all_translations(source_scale)
        print(f"SCALED_HKX_SKELETON_TRANSLATIONS factor={source_scale:g}", flush=True)

    if anim_name:
        entries = [anibnd.find_entry_matching_name(rf".*{anim_name}\.hkx(\.dcx)?$")]
    else:
        entries = sorted(anibnd.find_entries_matching_name(r"a.*\.hkx(\.dcx)?"), key=lambda entry: entry.name)
        if limit:
            entries = entries[:limit]

    if not entries:
        raise RuntimeError(f"No animation HKX entries found in {anibnd_path}")

    log = BatchLog()
    failures = []
    started = time.perf_counter()
    for index, entry in enumerate(entries, start=1):
        hkx_name = Path(entry.name).stem
        print(f"ANIM_PROGRESS {index}/{len(entries)} {hkx_name}", flush=True)
        try:
            animation = read_animation_hkx_entry(entry, compendium)
            if source_scale != 1.0 and not animation.animation_container.is_interleaved:
                animation = animation.to_interleaved_hkx()
            if source_scale != 1.0:
                try:
                    animation.animation_container.scale_all_translations(source_scale)
                except ValueError as ex:
                    if "No `extractedMotion` reference frame exists" not in str(ex):
                        raise
            SoulstructAnimation.new_from_hkx_animation(
                log,
                bpy.context,
                animation,
                skeleton,
                name=hkx_name,
                armature_obj=armature,
                model_name=character,
            )
        except Exception as ex:
            traceback.print_exc()
            failures.append((hkx_name, str(ex)))

    if failures:
        lines = "\n".join(f"- {name}: {error}" for name, error in failures)
        raise RuntimeError(f"Failed to import {len(failures)} animation(s):\n{lines}")

    print(f"Imported {len(entries)} animations in {time.perf_counter() - started:.1f}s", flush=True)
    if source_scale != 1.0:
        print(f"SCALED_HKX_ANIMATION_TRANSLATIONS {len(entries)} factor={source_scale:g}", flush=True)
    return entries


def iter_action_fcurves(action):
    seen = set()

    def yield_unique(fcurves):
        for fcurve in fcurves:
            pointer = fcurve.as_pointer()
            if pointer in seen:
                continue
            seen.add(pointer)
            yield fcurve

    yield from yield_unique(getattr(action, "fcurves", []))
    for layer in getattr(action, "layers", []):
        for strip in getattr(layer, "strips", []):
            for slot in getattr(action, "slots", []):
                channelbag = None
                try:
                    channelbag = strip.channelbag(slot, ensure=False)
                except TypeError:
                    try:
                        channelbag = strip.channelbag(slot)
                    except Exception:
                        channelbag = None
                except Exception:
                    channelbag = None
                if channelbag is not None:
                    yield from yield_unique(getattr(channelbag, "fcurves", []))


def animation_actions_for_armature():
    actions = []
    for action in bpy.data.actions:
        fcurves = list(iter_action_fcurves(action))
        if any(fcurve.data_path.startswith("pose.bones[") for fcurve in fcurves):
            actions.append(action)
    actions.sort(key=lambda action: action.name)
    return actions


def prepare_actions_for_export(armature):
    actions = animation_actions_for_armature()
    if not actions:
        raise RuntimeError("No armature animation actions were created.")
    animation_data = armature.animation_data_create()
    animation_data.action = actions[0]
    for track in list(animation_data.nla_tracks):
        animation_data.nla_tracks.remove(track)
    for action in actions:
        action.use_fake_user = True
    print(f"ACTIONS_FOR_EXPORT {len(actions)}", flush=True)
    print("NLA_STRIPS_FOR_EXPORT 0", flush=True)
    return actions


def get_bound_meshes(armature):
    meshes = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        if any(mod.type == "ARMATURE" and mod.object == armature for mod in obj.modifiers):
            meshes.append(obj)
            continue
        if obj.parent == armature and obj.vertex_groups:
            meshes.append(obj)
    if not meshes:
        raise RuntimeError(f"No meshes bound to armature {armature.name}")
    return meshes


def prepare_for_unreal(armature, source_scale: float):
    print("BLENDER_PROGRESS 3/5 preparing Unreal-friendly armature and scale", flush=True)
    meshes = get_bound_meshes(armature)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0

    bpy.ops.object.mode_set(mode="OBJECT") if bpy.ops.object.mode_set.poll() else None
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [armature, *meshes]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True, properties=True)

    print("SCALED_LOCATION_FCURVES 0 factor=1 reason=hkx_scaled_before_action_solve", flush=True)
    print(f"ARMATURE_OBJECT {armature.name}", flush=True)
    print(f"SOURCE_SCALE {source_scale:g}", flush=True)
    print(f"OBJECT_SCALE {tuple(round(v, 6) for v in armature.scale)}", flush=True)
    return meshes


def export_fbx(fbx_path: Path, armature, meshes, apply_scale_options: str):
    print("BLENDER_PROGRESS 4/5 exporting FBX", flush=True)
    fbx_path.parent.mkdir(parents=True, exist_ok=True)
    prepare_actions_for_export(armature)

    bpy.ops.object.mode_set(mode="OBJECT") if bpy.ops.object.mode_set.poll() else None
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [armature, *meshes]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature

    bpy.ops.export_scene.fbx(
        filepath=str(fbx_path),
        check_existing=False,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        use_custom_props=False,
        path_mode="COPY",
        embed_textures=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options=apply_scale_options,
        axis_forward="-Z",
        axis_up="Y",
        use_space_transform=True,
        bake_space_transform=False,
        mesh_smooth_type="FACE",
        use_subsurf=False,
        use_mesh_modifiers=True,
        use_mesh_edges=False,
        use_triangles=False,
        colors_type="SRGB",
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        use_armature_deform_only=False,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=False,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
    )
    if not fbx_path.is_file() or fbx_path.stat().st_size == 0:
        raise RuntimeError(f"FBX export failed or produced empty file: {fbx_path}")
    print(f"EXPORTED_FBX {fbx_path} size={fbx_path.stat().st_size} global_scale=1", flush=True)


def export_glb(glb_path: Path, armature, meshes):
    print("BLENDER_PROGRESS 4/5 exporting GLB", flush=True)
    glb_path.parent.mkdir(parents=True, exist_ok=True)
    prepare_actions_for_export(armature)

    bpy.ops.object.mode_set(mode="OBJECT") if bpy.ops.object.mode_set.poll() else None
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [armature, *meshes]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature

    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        check_existing=False,
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=False,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_texcoords=True,
        export_normals=True,
        export_tangents=False,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_nla_strips=False,
        export_frame_range=False,
        export_frame_step=1,
        export_force_sampling=True,
        export_bake_animation=False,
        export_skins=True,
        export_def_bones=False,
        export_all_influences=False,
        export_morph=False,
        export_cameras=False,
        export_lights=False,
    )
    if not glb_path.is_file() or glb_path.stat().st_size == 0:
        raise RuntimeError(f"GLB export failed or produced empty file: {glb_path}")
    print(f"EXPORTED_GLB {glb_path} size={glb_path.stat().st_size} export_yup=True", flush=True)


def main():
    args = parse_args()
    addon_root = Path(args.addon_root)
    flver = Path(args.flver)
    anibnd = Path(args.anibnd)
    output = Path(args.output)

    for path in (addon_root, flver, anibnd):
        if not path.exists():
            raise FileNotFoundError(path)

    register_soulstruct(addon_root)
    clear_scene()
    scaled_flver = make_scaled_flver(flver, args.source_scale)
    armature = import_flver(scaled_flver)
    normalize_armature_object_name(armature, args.armature_object_name)
    entries = import_animations(anibnd, armature, args.character, args.anim, args.limit, args.source_scale)
    meshes = prepare_for_unreal(armature, args.source_scale)
    if args.format == "fbx":
        export_fbx(output, armature, meshes, args.apply_scale_options)
    else:
        export_glb(output, armature, meshes)
    print(f"DONE animations={len(entries)} meshes={len(meshes)} {args.format}={output}", flush=True)


if __name__ == "__main__":
    main()

