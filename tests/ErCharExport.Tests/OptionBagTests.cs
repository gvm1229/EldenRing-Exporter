using ErCharExport;

namespace ErCharExport.Tests;

public sealed class OptionBagTests
{
    [Fact]
    public void Parse_ReadsValuesAndFlags()
    {
        var options = OptionBag.Parse(["--character", "c3181", "--skip-ue-script"]);

        Assert.Equal("c3181", options.Require("character"));
        Assert.True(options.HasFlag("skip-ue-script"));
    }
}

