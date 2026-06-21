namespace ErCharExport;

public static class PathHash
{
    public static string Normalize(string path)
    {
        if (path.Contains(':'))
            path = path[(path.IndexOf(':') + 1)..];
        path = path.ToLowerInvariant().Replace('\\', '/').Trim();
        return path.StartsWith('/') ? path : "/" + path;
    }

    public static ulong Compute(string path)
    {
        path = Normalize(path);
        ulong hash = 0;
        foreach (char c in path)
            hash = hash * 133 + c;
        return hash;
    }
}

