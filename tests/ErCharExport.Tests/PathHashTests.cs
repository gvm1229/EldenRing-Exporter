using ErCharExport;

namespace ErCharExport.Tests;

public sealed class PathHashTests
{
    [Fact]
    public void Normalize_LowercasesAndAddsLeadingSlash()
    {
        Assert.Equal("/chr/c3181.chrbnd.dcx", PathHash.Normalize(@"CHR\C3181.CHRBND.DCX"));
    }

    [Fact]
    public void Compute_IsStableForEquivalentPaths()
    {
        ulong a = PathHash.Compute("/chr/c3181.chrbnd.dcx");
        ulong b = PathHash.Compute(@"chr\C3181.CHRBND.DCX");

        Assert.Equal(a, b);
        Assert.NotEqual(0UL, a);
    }
}

