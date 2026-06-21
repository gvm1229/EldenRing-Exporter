namespace ErCharExport;

public sealed record ExportRequest(
    string CharacterId,
    Path GameDir,
    Path Blender,
    Path Witchy,
    Path NuxeResDir,
    Path SoulstructRoot,
    Path OutDir,
    string? AnimationBinder,
    string? SingleAnimation,
    int LimitAnimations,
    string TextureQuality,
    double SourceScale,
    bool SkipUnrealScript)
{
    public static ExportRequest FromOptions(OptionBag options)
    {
        string textureQuality = options.Get("texture-quality", "high").ToLowerInvariant();
        if (textureQuality is not ("high" or "low" or "none"))
            throw new CliException("--texture-quality must be high, low, or none.");

        return new ExportRequest(
            CharacterCatalog.NormalizeCharacterId(options.Require("character")),
            options.GetPath("game-dir", Defaults.GameDir),
            options.GetPath("blender", Defaults.Blender),
            options.GetPath("witchy", Defaults.Witchy),
            options.GetPath("nuxe-res", Defaults.NuxeResDir),
            options.GetPath("soulstruct", Defaults.Soulstruct),
            options.GetPath("out", Defaults.OutDir),
            options.GetOptional("animation-binder"),
            options.GetOptional("anim"),
            options.GetInt("limit-anims", 0),
            textureQuality,
            options.GetDouble("source-scale", 100.0),
            options.HasFlag("skip-ue-script"));
    }
}

