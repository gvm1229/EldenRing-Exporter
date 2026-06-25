using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErCharExport;

public sealed class ExportWorkflow
{
    public async Task ExportAsync(ExportRequest request)
    {
        RequireFile(request.Blender, "Blender executable");
        RequireFile(request.Witchy, "WitchyBND executable");
        RequireDirectory(request.SoulstructRoot, "Soulstruct Blender add-on root");

        var archive = Data3Archive.Load(request.GameDir, request.NuxeResDir);
        CharacterInfo character = CharacterCatalog.Inspect(archive.KnownPaths, request.CharacterId);
        ValidateCharacter(character);

        string animationBinder = SelectAnimationBinder(character, request.AnimationBinder);
        string? textureBinder = SelectTextureBinder(character, request.TextureQuality);

        Path characterOut = request.OutDir / request.CharacterId;
        Path rawOut = characterOut / "raw";
        Path exportOut = characterOut / "exports";
        Path textureOut = characterOut / "textures" / request.TextureQuality;
        Path ueOut = characterOut / "ue";

        Directory.CreateDirectory(characterOut.FullName);
        Directory.CreateDirectory(rawOut.FullName);
        Directory.CreateDirectory(exportOut.FullName);
        Directory.CreateDirectory(ueOut.FullName);

        var pathsToExtract = new List<string> { character.Chrbnds.Single() };
        pathsToExtract.AddRange(character.Anibnds);
        if (textureBinder is not null)
            pathsToExtract.Add(textureBinder);

        Console.WriteLine("STEP 1/5 extracting binders from Data3");
        IReadOnlyList<ExtractedFile> extracted = archive.ExtractPaths(pathsToExtract, rawOut);

        Path chrbnd = LocalPathFor(rawOut, character.Chrbnds.Single());
        Path anibnd = LocalPathFor(rawOut, animationBinder);
        Path? texbnd = null;
        if (textureBinder is not null)
            texbnd = LocalPathFor(rawOut, textureBinder);

        Console.WriteLine("STEP 2/5 unpacking character binder");
        await RunWitchyAsync(request.Witchy, chrbnd);
        Path flver = FindRequiredFile(chrbnd.Parent / $"{request.CharacterId}-chrbnd-dcx", "*.flver", "FLVER");

        IReadOnlyList<Path> textures = Array.Empty<Path>();
        if (texbnd is not null)
        {
            Console.WriteLine("STEP 3/5 unpacking texture binder");
            textures = await ExtractTexturesAsync(request.Witchy, texbnd.Value, textureOut);
        }
        else
        {
            Console.WriteLine("STEP 3/5 skipping textures");
        }

        string artifactExtension = request.ExportFormat;
        string artifactLabel = request.ExportFormat.ToUpperInvariant();
        Console.WriteLine($"STEP 4/5 exporting {artifactLabel} with Blender");
        Path exportedAsset = exportOut / $"{request.CharacterId}_ue5.{artifactExtension}";
        Path blenderLog = exportOut / $"{request.CharacterId}_ue5.blender.log";
        await RunBlenderExportAsync(request, flver, anibnd, exportedAsset, blenderLog);

        Path? unrealScript = null;
        if (!request.SkipUnrealScript && request.ExportFormat == "fbx")
        {
            Console.WriteLine("STEP 5/5 generating Unreal import script");
            unrealScript = ueOut / $"import_{request.CharacterId}_to_unreal.py";
            await File.WriteAllTextAsync(unrealScript.Value.FullName, UnrealScriptGenerator.Generate(request.CharacterId, exportedAsset, textureOut, request.TextureQuality));
        }
        else if (!request.SkipUnrealScript)
        {
            Console.WriteLine("STEP 5/5 skipping Unreal import script for GLB export");
        }
        else
        {
            Console.WriteLine("STEP 5/5 skipping Unreal import script");
        }

        var manifest = new ExportManifest(
            request.CharacterId,
            DateTimeOffset.UtcNow,
            request.SourceScale,
            request.ExportFormat,
            character.Chrbnds.Single(),
            animationBinder,
            textureBinder,
            exportedAsset.FullName,
            blenderLog.FullName,
            textures.Select(path => path.FullName).Order().ToArray(),
            unrealScript?.FullName,
            extracted);

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        await File.WriteAllTextAsync((characterOut / "manifest.json").FullName, JsonSerializer.Serialize(manifest, jsonOptions));

        Console.WriteLine("DONE");
        Console.WriteLine($"{artifactLabel}={exportedAsset}");
        Console.WriteLine($"BLENDER_LOG={blenderLog}");
        if (unrealScript is not null)
            Console.WriteLine($"UNREAL_SCRIPT={unrealScript}");
    }

    private static void ValidateCharacter(CharacterInfo character)
    {
        if (!character.HasMesh)
            throw new CliException($"No exact mesh binder found for {character.Id}: /chr/{character.Id}.chrbnd.dcx");
        if (!character.HasAnimations)
            throw new CliException($"No animation binders found for {character.Id}.");
    }

    private static string SelectAnimationBinder(CharacterInfo character, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            string normalized = NormalizeBinderChoice(requested, character.Id, "anibnd");
            if (!character.Anibnds.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                throw new CliException($"Animation binder '{requested}' was not found for {character.Id}.");
            return normalized;
        }

        if (character.Anibnds.Count == 1)
            return character.Anibnds.Single();

        string baseBinder = $"/chr/{character.Id}.anibnd.dcx";
        string? exactBase = character.Anibnds.FirstOrDefault(path => string.Equals(path, baseBinder, StringComparison.OrdinalIgnoreCase));
        if (exactBase is not null)
            return exactBase;

        string choices = string.Join(Environment.NewLine, character.Anibnds.Select(path => $"  {path}"));
        throw new CliException($"Multiple animation binders found for {character.Id} and no exact base binder exists. Re-run export with --animation-binder:{Environment.NewLine}{choices}");
    }

    private static string? SelectTextureBinder(CharacterInfo character, string textureQuality)
    {
        return textureQuality switch
        {
            "none" => null,
            "high" when character.HighTexbnds.Count > 0 => character.HighTexbnds.Single(),
            "low" when character.LowTexbnds.Count > 0 => character.LowTexbnds.Single(),
            "high" => null,
            "low" => null,
            _ => throw new CliException($"Unsupported texture quality: {textureQuality}")
        };
    }

    private static string NormalizeBinderChoice(string value, string characterId, string binderKind)
    {
        value = value.Trim().Replace('\\', '/').ToLowerInvariant();
        if (value.StartsWith("/chr/"))
            return PathHash.Normalize(value);
        if (!value.EndsWith(".dcx"))
            value += $".{binderKind}.dcx";
        return PathHash.Normalize($"/chr/{value}");
    }

    private static async Task RunWitchyAsync(Path witchy, Path target)
    {
        var result = await ProcessRunner.RunAsync(witchy.FullName, new[] { target.FullName });
        if (result.ExitCode != 0)
            throw new CliException($"WitchyBND failed for {target} with exit code {result.ExitCode}.");
    }

    private static async Task<IReadOnlyList<Path>> ExtractTexturesAsync(Path witchy, Path texbnd, Path textureOut)
    {
        await RunWitchyAsync(witchy, texbnd);
        Path texbndDir = new(System.IO.Path.Combine(texbnd.Parent.FullName, System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetFileNameWithoutExtension(texbnd.FullName)).Replace(".texbnd", "-texbnd") + "-dcx"));
        if (!Directory.Exists(texbndDir.FullName))
            texbndDir = FindRequiredDirectory(texbnd.Parent, "*texbnd-dcx", "texture binder directory");

        Path tpf = FindRequiredFile(texbndDir, "*.tpf", "TPF");
        await RunWitchyAsync(witchy, tpf);

        Path tpfDir = FindRequiredDirectory(texbndDir, "*-tpf", "unpacked TPF directory");
        Directory.CreateDirectory(textureOut.FullName);

        var copied = new List<Path>();
        foreach (string dds in Directory.EnumerateFiles(tpfDir.FullName, "*.dds"))
        {
            Path dest = textureOut / System.IO.Path.GetFileName(dds);
            File.Copy(dds, dest.FullName, overwrite: true);
            copied.Add(dest);
        }
        Console.WriteLine($"COPIED_TEXTURES {copied.Count} dir={textureOut}");
        return copied;
    }

    private static async Task RunBlenderExportAsync(ExportRequest request, Path flver, Path anibnd, Path output, Path log)
    {
        Path script = new Path(AppContext.BaseDirectory) / "scripts" / "blender_export_character.py";
        RequireFile(script, "bundled Blender export script");

        var args = new List<string>
        {
            "--background",
            "--factory-startup",
            "--python", script.FullName,
            "--",
            "--addon-root", request.SoulstructRoot.FullName,
            "--flver", flver.FullName,
            "--anibnd", anibnd.FullName,
            "--output", output.FullName,
            "--format", request.ExportFormat,
            "--character", request.CharacterId,
            "--source-scale", request.SourceScale.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--armature-object-name", "root",
            "--apply-scale-options", "FBX_SCALE_UNITS",
        };

        if (!string.IsNullOrWhiteSpace(request.SingleAnimation))
            args.AddRange(new[] { "--anim", request.SingleAnimation });
        if (request.LimitAnimations > 0)
            args.AddRange(new[] { "--limit", request.LimitAnimations.ToString(System.Globalization.CultureInfo.InvariantCulture) });

        var result = await ProcessRunner.RunAsync(request.Blender.FullName, args, logPath: log);
        if (result.ExitCode != 0)
            throw new CliException($"Blender export failed with exit code {result.ExitCode}. Log: {log}");
        if (!File.Exists(output.FullName) || new FileInfo(output.FullName).Length == 0)
            throw new CliException($"Blender did not create a valid {request.ExportFormat.ToUpperInvariant()}: {output}. Log: {log}");
        string successMarker = $"EXPORTED_{request.ExportFormat.ToUpperInvariant()}";
        if (!result.Output.Contains(successMarker, StringComparison.Ordinal))
            throw new CliException($"Blender finished without the {successMarker} success marker. Log: {log}");
        if (ContainsFatalBlenderImportError(result.Output))
            throw new CliException($"Blender reported an import/export exception. Log: {log}");
    }

    private static bool ContainsFatalBlenderImportError(string output)
    {
        string[] fatalMarkers =
        [
            "FileNotFoundError:",
            "EntryNotFoundError:",
            "RuntimeError: Failed to import",
            "RuntimeError: Failed to export",
            "Traceback (most recent call last):\r\n  File \"",
        ];

        if (output.Contains("EXPORTED_", StringComparison.Ordinal) &&
            !output.Contains("FileNotFoundError:", StringComparison.Ordinal) &&
            !output.Contains("EntryNotFoundError:", StringComparison.Ordinal) &&
            !output.Contains("RuntimeError: Failed to import", StringComparison.Ordinal) &&
            !output.Contains("RuntimeError: Failed to export", StringComparison.Ordinal))
        {
            return false;
        }

        return fatalMarkers.Any(marker => output.Contains(marker, StringComparison.Ordinal));
    }

    private static Path LocalPathFor(Path rawOut, string gamePath)
        => rawOut / PathHash.Normalize(gamePath).TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar);

    private static Path FindRequiredFile(Path directory, string pattern, string label)
    {
        if (!Directory.Exists(directory.FullName))
            throw new CliException($"Missing {label} directory: {directory}");
        string? found = Directory.EnumerateFiles(directory.FullName, pattern, SearchOption.TopDirectoryOnly).Order().FirstOrDefault();
        return found is not null ? new Path(found) : throw new CliException($"Could not find {label} in {directory} matching {pattern}.");
    }

    private static Path FindRequiredDirectory(Path directory, string pattern, string label)
    {
        string? found = Directory.EnumerateDirectories(directory.FullName, pattern, SearchOption.TopDirectoryOnly).Order().FirstOrDefault();
        return found is not null ? new Path(found) : throw new CliException($"Could not find {label} in {directory} matching {pattern}.");
    }

    private static void RequireFile(Path path, string label)
    {
        if (!File.Exists(path.FullName))
            throw new CliException($"Missing {label}: {path}");
    }

    private static void RequireDirectory(Path path, string label)
    {
        if (!Directory.Exists(path.FullName))
            throw new CliException($"Missing {label}: {path}");
    }
}

public sealed record ExportManifest(
    string CharacterId,
    DateTimeOffset CreatedUtc,
    double SourceScale,
    string ExportFormat,
    string Chrbnd,
    string Anibnd,
    string? Texbnd,
    string ExportedAsset,
    string BlenderLog,
    IReadOnlyList<string> Textures,
    string? UnrealScript,
    IReadOnlyList<ExtractedFile> ExtractedFiles);
