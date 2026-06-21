using ErCharExport;

namespace ErCharExport.Tests;

public sealed class UnrealScriptGeneratorTests
{
    [Fact]
    public void Generate_IncludesCorePathsAndMaterialLogic()
    {
        string script = UnrealScriptGenerator.Generate(
            "c3181",
            @"D:\exports\c3181\exports\c3181_ue5.fbx",
            @"D:\exports\c3181\textures\high",
            "high");

        Assert.Contains("CHARACTER_ID = \"c3181\"", script);
        Assert.Contains("FBX_PATH", script);
        Assert.Contains("TextureCompressionSettings.TC_NORMALMAP", script);
        Assert.Contains("create_material(\"M_body\"", script);
    }
}

