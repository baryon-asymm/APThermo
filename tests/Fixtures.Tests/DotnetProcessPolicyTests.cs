using APThermo.Harness;

namespace APThermo.Fixtures.Tests;

/// <summary>The immutable process policy retains the documented defaults.</summary>
public sealed class DotnetProcessPolicyTests
{
    /// <summary>Diagnostic consumers can read exactly the unchanged limits used by the runner.</summary>
    [Fact]
    public void DefaultsRemainDocumentedAndReadOnly()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), DotnetProcess.DefaultTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), DotnetProcess.CleanupTimeout);
        foreach (var name in new[] { nameof(DotnetProcess.DefaultTimeout), nameof(DotnetProcess.CleanupTimeout) })
        {
            var property = typeof(DotnetProcess).GetProperty(name)!;
            Assert.NotNull(property.GetMethod);
            Assert.True(property.GetMethod.IsStatic);
            Assert.Null(property.SetMethod);
        }
    }
}
