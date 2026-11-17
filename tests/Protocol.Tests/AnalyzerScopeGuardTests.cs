using System.Reflection;

namespace APThermo.Protocol.Tests;

/// <summary>Analyzer options must not restrict which symbols are diagnosed.</summary>
public sealed class AnalyzerScopeGuardTests
{
    /// <summary>Restricted surfaces and excluded names are refused; an all-surface value is safe.</summary>
    [Theory]
    [InlineData("dotnet_code_quality.CA1822.api_surface", "private", true)]
    [InlineData("DOTNET_CODE_QUALITY.CA1822.API_SURFACE", " Public, Internal ", true)]
    [InlineData("dotnet_code_quality.api_surface", " all ", false)]
    [InlineData("dotnet_code_quality.CA1822.api_surface", "ALL", false)]
    [InlineData("dotnet_code_quality.CA1062.excluded_symbol_names", "Foo", true)]
    [InlineData("DOTNET_CODE_QUALITY.EXCLUDED_SYMBOL_NAMES", " Foo | Bar ", true)]
    [InlineData("dotnet_code_quality.CA1062.excluded_symbol_names", " ", false)]
    public void AnalyzerScopeOptionIsChecked(string key, string value, bool refused)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"root = true\n[*.cs]\n{key} = {value}\n");
            var check = typeof(DiagnosticsTests).GetMethod("SeverityProblems", BindingFlags.NonPublic | BindingFlags.Static);
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
