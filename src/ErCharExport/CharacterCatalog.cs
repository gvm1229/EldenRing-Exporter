using System.Text.RegularExpressions;

namespace ErCharExport;

public sealed record CharacterInfo(
    string Id,
    IReadOnlyList<string> Chrbnds,
    IReadOnlyList<string> Anibnds,
    IReadOnlyList<string> Behbnds,
    IReadOnlyList<string> HighTexbnds,
    IReadOnlyList<string> LowTexbnds)
{
    public bool HasMesh => Chrbnds.Count > 0;
    public bool HasAnimations => Anibnds.Count > 0;
    public bool HasHighTextures => HighTexbnds.Count > 0;
    public bool HasLowTextures => LowTexbnds.Count > 0;
}

public static partial class CharacterCatalog
{
    public static IReadOnlyList<CharacterInfo> Discover(IEnumerable<string> archivePaths)
    {
        var paths = archivePaths.Select(PathHash.Normalize).Where(path => path.StartsWith("/chr/")).Distinct().ToArray();
        var ids = paths.Select(path => CharacterIdRegex().Match(path))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .Order()
            .ToArray();

        return ids.Select(id => Inspect(paths, id)).ToArray();
    }

    public static CharacterInfo Inspect(IEnumerable<string> archivePaths, string characterId)
    {
        characterId = NormalizeCharacterId(characterId);
        var paths = archivePaths.Select(PathHash.Normalize).Where(path => path.StartsWith($"/chr/{characterId}")).Distinct().Order().ToArray();

        return new CharacterInfo(
            characterId,
            paths.Where(path => path == $"/chr/{characterId}.chrbnd.dcx").ToArray(),
            paths.Where(path => path.EndsWith(".anibnd.dcx")).ToArray(),
            paths.Where(path => path == $"/chr/{characterId}.behbnd.dcx").ToArray(),
            paths.Where(path => path == $"/chr/{characterId}_h.texbnd.dcx").ToArray(),
            paths.Where(path => path == $"/chr/{characterId}_l.texbnd.dcx").ToArray());
    }

    public static string NormalizeCharacterId(string value)
    {
        value = value.Trim().ToLowerInvariant();
        if (!CharacterIdOnlyRegex().IsMatch(value))
            throw new CliException($"Invalid character ID '{value}'. Expected format like c3181.");
        return value;
    }

    [GeneratedRegex("^/chr/(c\\d{4})")]
    private static partial Regex CharacterIdRegex();

    [GeneratedRegex("^c\\d{4}$")]
    private static partial Regex CharacterIdOnlyRegex();
}

