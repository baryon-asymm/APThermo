using System.Diagnostics;

namespace APThermo.Protocol.Tests;

/// <summary>xUnit setup can start a process even when the test body does not.</summary>
public sealed class ProcessSetupGuardTests
{
    /// <summary>Constructor setup is attributed to its consuming method.</summary>
    [Fact]
    [Trait("Category", "EndToEnd")]
    public void AutomaticallyRunConstructorsAreIncluded()
    {
        GC.KeepAlive(new ConstructorConsumer());
        Assert.True(ProcessStarts.StartsAProcess(typeof(ConstructorConsumer).GetMethod(nameof(ConstructorConsumer.Body))!));
    }

    /// <summary>Class-fixture setup is attributed to each consuming method.</summary>
    [Fact]
    [Trait("Category", "EndToEnd")]
    public void AutomaticallyRunFixtureConstructorsAreIncluded()
    {
        GC.KeepAlive(new FixtureConsumer());
        GC.KeepAlive(new ProcessFixture());
        Assert.True(ProcessStarts.StartsAProcess(typeof(FixtureConsumer).GetMethod(nameof(FixtureConsumer.Body))!));
    }

    /// <summary>xUnit fixture asynchronous initialization is also attributed to the consumer.</summary>
    [Fact]
    [Trait("Category", "EndToEnd")]
    public void AutomaticallyRunFixtureInitializationIsIncluded()
    {
        GC.KeepAlive(new AsyncFixture());
        GC.KeepAlive(new AsyncConsumer());
        Assert.True(ProcessStarts.StartsAProcess(typeof(AsyncConsumer).GetMethod(nameof(AsyncConsumer.Body))!));
    }

    /// <summary>Synchronous fixture teardown is included alongside initialization.</summary>
    [Fact]
    [Trait("Category", "EndToEnd")]
    public void AutomaticallyRunSynchronousFixtureTeardownIsIncluded()
    {
        using var fixture = new DisposeFixture();
        GC.KeepAlive(fixture);
        GC.KeepAlive(new DisposeConsumer());
        Assert.True(ProcessStarts.StartsAProcess(typeof(DisposeConsumer).GetMethod(nameof(DisposeConsumer.Body))!));
    }

    /// <summary>Asynchronous fixture teardown is included independently.</summary>
    [Fact]
    [Trait("Category", "EndToEnd")]
    public void AutomaticallyRunAsynchronousFixtureTeardownIsIncluded()
    {
        GC.KeepAlive(new AsyncDisposeFixture());
        GC.KeepAlive(new AsyncDisposeConsumer());
        Assert.True(ProcessStarts.StartsAProcess(typeof(AsyncDisposeConsumer).GetMethod(nameof(AsyncDisposeConsumer.Body))!));
    }

    /// <summary>Real emitted collection metadata distinguishes active and harmless fixture setup.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "EndToEnd")]
    public void CollectionFixtureSetupIsIncluded(bool starts)
    {
        var method = ProcessFixtureAssembly.Create(starts);
        Assert.Equal(starts, ProcessStarts.StartsAProcess(method, method.Module.Assembly));
    }

    /// <summary>Harmless setup does not acquire a spurious process edge.</summary>
    [Fact]
    public void HarmlessSetupIsNotMarkedAsAProcessStart()
    {
        GC.KeepAlive(new PlainConsumer());
        Assert.False(ProcessStarts.StartsAProcess(typeof(PlainConsumer).GetMethod(nameof(PlainConsumer.Body))!));
    }

    private sealed class ConstructorConsumer
    {
        public ConstructorConsumer() { GC.KeepAlive(new ProcessStartInfo("controlled")); }
        public void Body() => GC.KeepAlive(this);
    }

    /// <summary>A controlled fixture that constructs a process start descriptor.</summary>
    private sealed class ProcessFixture
    {
        public ProcessFixture() { GC.KeepAlive(new ProcessStartInfo("controlled")); }
    }

    private sealed class FixtureConsumer : IClassFixture<ProcessFixture>
    {
        public void Body() => GC.KeepAlive(this);
    }

    private sealed class AsyncFixture : IAsyncLifetime
    {
        public Task InitializeAsync()
        {
            GC.KeepAlive(new ProcessStartInfo("controlled"));
            return Task.CompletedTask;
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private sealed class AsyncConsumer : IClassFixture<AsyncFixture>
    {
        public void Body() => GC.KeepAlive(this);
    }

    private sealed class DisposeFixture : IDisposable
    {
        public void Dispose() => GC.KeepAlive(new ProcessStartInfo("controlled"));
    }

    private sealed class DisposeConsumer : IClassFixture<DisposeFixture>
    {
        public void Body() => GC.KeepAlive(this);
    }

    private sealed class AsyncDisposeFixture : IAsyncLifetime
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public Task DisposeAsync()
        {
            GC.KeepAlive(new ProcessStartInfo("controlled"));
            return Task.CompletedTask;
        }
    }

    private sealed class AsyncDisposeConsumer : IClassFixture<AsyncDisposeFixture>
    {
        public void Body() => GC.KeepAlive(this);
    }

    private sealed class PlainConsumer
    {
        public void Body() => GC.KeepAlive(this);
    }
}
