namespace ErCharExport;

public static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintHelp();
            return 0;
        }

        string command = args[0].ToLowerInvariant();
        var options = OptionBag.Parse(args.Skip(1).ToArray());

        return command switch
        {
            "list" => await RunListAsync(options),
            "inspect" => await RunInspectAsync(options),
            "export" => await RunExportAsync(options),
            _ => throw new CliException($"Unknown command '{command}'.")
        };
    }

    private static Task<int> RunListAsync(OptionBag options)
    {
        var archive = Data3Archive.Load(
            options.GetPath("game-dir", Defaults.GameDir),
            options.GetPath("nuxe-res", Defaults.NuxeResDir));

        Console.WriteLine("ID     Mesh Anims HighTex LowTex");
        foreach (var character in CharacterCatalog.Discover(archive.KnownPaths).Where(character => character.HasMesh))
        {
            Console.WriteLine(
                $"{character.Id,-6} " +
                $"{YesNo(character.HasMesh),-4} " +
                $"{character.Anibnds.Count,-5} " +
                $"{YesNo(character.HasHighTextures),-7} " +
                $"{YesNo(character.HasLowTextures),-6}");
        }
        return Task.FromResult(0);
    }

    private static Task<int> RunInspectAsync(OptionBag options)
    {
        string characterId = options.Require("character");
        var archive = Data3Archive.Load(
            options.GetPath("game-dir", Defaults.GameDir),
            options.GetPath("nuxe-res", Defaults.NuxeResDir));
        var character = CharacterCatalog.Inspect(archive.KnownPaths, characterId);
        PrintCharacter(character);
        return Task.FromResult(0);
    }

    private static async Task<int> RunExportAsync(OptionBag options)
    {
        var request = ExportRequest.FromOptions(options);
        await new ExportWorkflow().ExportAsync(request);
        return 0;
    }

    private static void PrintCharacter(CharacterInfo character)
    {
        Console.WriteLine($"Character: {character.Id}");
        PrintList("CHRBND", character.Chrbnds);
        PrintList("ANIBND", character.Anibnds);
        PrintList("BEHBND", character.Behbnds);
        PrintList("High TEXBND", character.HighTexbnds);
        PrintList("Low TEXBND", character.LowTexbnds);
    }

    private static void PrintList(string label, IReadOnlyList<string> values)
    {
        Console.WriteLine($"{label}: {(values.Count == 0 ? "(none)" : "")}");
        foreach (string value in values)
            Console.WriteLine($"  {value}");
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static void PrintHelp()
    {
        Console.WriteLine("""
        er-char-export

        Commands:
          list      --game-dir <path> [--nuxe-res <path>]
          inspect   --character c3181 --game-dir <path> [--nuxe-res <path>]
          export    --character c3181 --game-dir <path> --blender <path> --witchy <path> --out <path>

        Export options:
          --animation-binder <path-or-name>
          --anim <a000_001020>
          --limit-anims <count>
          --texture-quality high|low|none
          --source-scale 100
          --skip-ue-script
          --soulstruct <io_soulstruct addon root>
        """);
    }
}

public sealed class OptionBag
{
    private readonly Dictionary<string, string?> _values;

    private OptionBag(Dictionary<string, string?> values)
    {
        _values = values;
    }

    public static OptionBag Parse(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--"))
                throw new CliException($"Unexpected argument '{arg}'. Options must start with --.");

            string key = arg[2..];
            if (string.IsNullOrWhiteSpace(key))
                throw new CliException("Empty option name.");

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--"))
            {
                values[key] = null;
                continue;
            }

            values[key] = args[++i];
        }
        return new OptionBag(values);
    }

    public string Require(string key)
    {
        if (!_values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            throw new CliException($"Missing required option --{key}.");
        return value;
    }

    public string Get(string key, string defaultValue)
        => _values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : defaultValue;

    public Path GetPath(string key, string defaultValue) => Path.GetFullPath(Get(key, defaultValue));

    public bool HasFlag(string key) => _values.ContainsKey(key) && string.IsNullOrWhiteSpace(_values[key]);

    public int GetInt(string key, int defaultValue)
    {
        if (!_values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            return defaultValue;
        return int.TryParse(value, out int parsed) ? parsed : throw new CliException($"--{key} must be an integer.");
    }

    public double GetDouble(string key, double defaultValue)
    {
        if (!_values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            return defaultValue;
        return double.TryParse(value, out double parsed) ? parsed : throw new CliException($"--{key} must be a number.");
    }

    public string? GetOptional(string key)
        => _values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}

public static class Defaults
{
    public const string GameDir = @"D:\SteamLibrary\steamapps\common\ELDEN RING\Game";
    public const string Blender = @"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe";
    public const string Witchy = @"D:\RE_EXTRACT\ELDEN_RING_EXTRACT\tools\WitchyBND-v3.0.0.1-win-x64\WitchyBND.exe";
    public const string NuxeResDir = @"D:\RE_EXTRACT\ELDEN_RING_EXTRACT\tools\Nuxe.1.2.0\Nuxe 1.2.0\res";
    public const string Soulstruct = @"D:\RE_EXTRACT\ELDEN_RING_EXTRACT\tools\io_soulstruct-2.6.0";
    public const string OutDir = @"D:\RE_EXTRACT\ELDEN_RING_EXTRACT\exports";
}

