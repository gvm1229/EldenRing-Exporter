using Coremats;
using System.Security.Cryptography;

namespace ErCharExport;

public sealed record ExtractedFile(string GamePath, string LocalPath, long Length);

public sealed class Data3Archive
{
    private readonly Path _gameDir;
    private readonly Dictionary<ulong, string> _dictionary;
    private readonly Dictionary<ulong, ArchiveEntry> _entries;

    private Data3Archive(Path gameDir, Dictionary<ulong, string> dictionary, Dictionary<ulong, ArchiveEntry> entries)
    {
        _gameDir = gameDir;
        _dictionary = dictionary;
        _entries = entries;
    }

    public IReadOnlyList<string> KnownPaths => _dictionary.Values.Order().ToArray();

    public static Data3Archive Load(Path gameDir, Path nuxeResDir)
    {
        Path bhdPath = gameDir / "Data3.bhd";
        Path bdtPath = gameDir / "Data3.bdt";
        Path keyPath = nuxeResDir / "BinderKeys" / "EldenRing_PC" / "Key" / "Data3.pem";
        Path dictPath = nuxeResDir / "BinderKeys" / "EldenRing_PC" / "Hash" / "Data3.txt";

        RequireFile(bhdPath);
        RequireFile(bdtPath);
        RequireFile(keyPath);
        RequireFile(dictPath);

        byte[] bhdBytes = File.ReadAllBytes(bhdPath);
        if (!BHD5.Is(bhdBytes))
            bhdBytes = Coremats.Crypto.Binder5.DecryptBhd(bhdBytes, File.ReadAllText(keyPath));

        BHD5 header = BHD5.Read(bhdBytes, BHD5.Bhd5Format.EldenRing);
        var dictionary = File.ReadLines(dictPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(PathHash.Normalize)
            .Distinct()
            .ToDictionary(PathHash.Compute, path => path);

        var entries = header.Buckets
            .SelectMany(bucket => bucket)
            .Select(file => new ArchiveEntry(
                file.PathHash,
                file.DataOffset,
                file.DataLength,
                file.UnpaddedDataLength,
                file.Encryption))
            .ToDictionary(entry => entry.PathHash);

        return new Data3Archive(gameDir, dictionary, entries);
    }

    public bool ContainsPath(string gamePath) => _entries.ContainsKey(PathHash.Compute(gamePath));

    public IReadOnlyList<ExtractedFile> ExtractPaths(IEnumerable<string> gamePaths, Path outDir)
    {
        Path bdtPath = _gameDir / "Data3.bdt";
        RequireFile(bdtPath);
        Directory.CreateDirectory(outDir);

        using var bdt = File.OpenRead(bdtPath);
        var extracted = new List<ExtractedFile>();

        foreach (string requestedPath in gamePaths.Select(PathHash.Normalize).Distinct())
        {
            ulong hash = PathHash.Compute(requestedPath);
            if (!_entries.TryGetValue(hash, out ArchiveEntry? entry))
                throw new CliException($"Archive path not found in Data3: {requestedPath}");

            string gamePath = _dictionary.GetValueOrDefault(hash, requestedPath);
            Path localPath = outDir / gamePath.TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar);
            Directory.CreateDirectory(localPath.Parent.FullName);

            byte[] buffer = new byte[entry.DataLength];
            bdt.Position = entry.DataOffset;
            bdt.ReadExactly(buffer);

            if (entry.Encryption is not null)
                DecryptFile(entry.Encryption, buffer);

            int finalLength = entry.UnpaddedDataLength != 0 ? entry.UnpaddedDataLength : entry.DataLength;
            File.WriteAllBytes(localPath, buffer.AsSpan(0, finalLength));
            extracted.Add(new ExtractedFile(gamePath, localPath.FullName, finalLength));
        }

        return extracted;
    }

    private static void RequireFile(Path path)
    {
        if (!File.Exists(path))
            throw new CliException($"Required file not found: {path}");
    }

    private static void DecryptFile(BHD5.FileEncryption encryption, byte[] buffer)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = encryption.Key;
        aes.IV = new byte[16];

        foreach (BHD5.Range range in encryption.Ranges.Where(range => range.Start != -1 && range.End != -1 && range.Start != range.End))
        {
            int start = (int)range.Start;
            int count = (int)(range.End - range.Start);
            var span = buffer.AsSpan(start, count);
            aes.DecryptEcb(span, span, aes.Padding);
        }
    }

    private sealed record ArchiveEntry(
        ulong PathHash,
        long DataOffset,
        int DataLength,
        int UnpaddedDataLength,
        BHD5.FileEncryption? Encryption);
}

public readonly record struct Path(string FullName)
{
    public static implicit operator Path(string value) => new(System.IO.Path.GetFullPath(value));
    public static implicit operator string(Path value) => value.FullName;
    public static Path GetFullPath(string value) => new(System.IO.Path.GetFullPath(value));
    public static Path operator /(Path left, string right) => new(System.IO.Path.Combine(left.FullName, right));
    public Path Parent => System.IO.Path.GetDirectoryName(FullName) is { } parent
        ? new Path(parent)
        : throw new InvalidOperationException($"Path has no parent: {FullName}");
    public override string ToString() => FullName;
}
