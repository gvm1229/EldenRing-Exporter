using ErCharExport;

namespace ErCharExport.Tests;

public sealed class CharacterCatalogTests
{
    private static readonly string[] Paths =
    [
        "/chr/c3181.chrbnd.dcx",
        "/chr/c3181.anibnd.dcx",
        "/chr/c3181.behbnd.dcx",
        "/chr/c3181_h.texbnd.dcx",
        "/chr/c3181_l.texbnd.dcx",
        "/chr/c0000.anibnd.dcx",
        "/chr/c0000_a00_hi.anibnd.dcx",
        "/chr/c0000.chrbnd.dcx",
    ];

    [Fact]
    public void Inspect_FindsExpectedRedWolfBinders()
    {
        CharacterInfo info = CharacterCatalog.Inspect(Paths, "c3181");

        Assert.Single(info.Chrbnds);
        Assert.Single(info.Anibnds);
        Assert.Single(info.Behbnds);
        Assert.Single(info.HighTexbnds);
        Assert.Single(info.LowTexbnds);
    }

    [Fact]
    public void Inspect_FindsMultipleAnimationBinders()
    {
        CharacterInfo info = CharacterCatalog.Inspect(Paths, "c0000");

        Assert.Equal(2, info.Anibnds.Count);
    }

    [Fact]
    public void Discover_ReturnsCharacterIds()
    {
        var ids = CharacterCatalog.Discover(Paths).Select(info => info.Id).ToArray();

        Assert.Contains("c3181", ids);
        Assert.Contains("c0000", ids);
    }
}

