using System.Reflection;

namespace APThermo.Protocol.Tests;

/// <summary>MSBuild's warning redirection channels may not mask diagnostics.</summary>
public sealed class MsBuildWarningChannelsTests
{
    /// <summary>Each setting is rejected independently; a harmless property is accepted.</summary>
    [Theory]
    [InlineData("MSBuildWarningsAsMessages", true)]
    [InlineData("MSBuildWarningsNotAsErrors", true)]
    [InlineData("msbuildwarningsasmessages", true)]
    [InlineData("nowarn", true)]
    [InlineData("AssemblyName", false)]
    public void WarningChannelIsReported(string property, bool refused)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"<Project><PropertyGroup><{property}>value</{property}></PropertyGroup></Project>");
            var check = typeof(DiagnosticsTests).GetMethod("BuildFileProblems", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(check);
            var issues = ((IEnumerable<string>)check.Invoke(null, [path])!).ToArray();
            Assert.Equal(refused, issues.Length > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
