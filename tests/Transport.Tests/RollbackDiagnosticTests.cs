using System.Collections;
using System.Collections.ObjectModel;

namespace APThermo.Transport.Tests;

/// <summary>Recoverable reporting failures must not replace the actual primary or prevent cleanup.</summary>
public sealed class RollbackDiagnosticTests
{
    private sealed class Resource(bool fails) : IDisposable
    {
        internal int Attempts { get; private set; }

        public void Dispose()
        {
            Attempts++;
            if (fails && Attempts == 1)
            {
                throw new InvalidOperationException("controlled cleanup failure");
            }
        }
    }

    private sealed class DiagnosticDictionary(Exception failure) : Hashtable
    {
        public override object? this[object key]
        {
            get => base[key];
            set => throw failure;
        }
    }

    private sealed class PrimaryFailure : Exception
    {
        private readonly IDictionary _diagnostics = new Hashtable();
        private readonly Exception? _getterFailure;

        public PrimaryFailure() { }

        public PrimaryFailure(string message) : base(message) { }

        public PrimaryFailure(string message, Exception innerException) : base(message, innerException) { }

        internal PrimaryFailure(IDictionary diagnostics, Exception? getterFailure = null) : base("controlled primary failure")
        {
            _diagnostics = diagnostics;
            _getterFailure = getterFailure;
        }

        public override IDictionary Data => _getterFailure is null ? _diagnostics : throw _getterFailure;
    }

    /// <summary>The exact original exception survives ordinary, read-only and throwing diagnostic storage.</summary>
    [Theory]
    [InlineData("ordinary")]
    [InlineData("readOnly")]
    [InlineData("getter")]
    [InlineData("setter")]
    [InlineData("oomGetter")]
    [InlineData("oomSetter")]
    public void DiagnosticStorageCannotReplaceThePrimary(string storage)
    {
        using var first = new Resource(fails: true);
        using var last = new Resource(fails: false);
        using var owner = new TransportBufferOwnership();
        var primary = Primary(storage);
        var actual = Record.Exception(() =>
        {
            _ = owner.Build<int>(tracking =>
            {
                _ = tracking.Acquire(() => first);
                _ = tracking.Acquire(() => last);
                throw primary;
            });
        });
        Assert.Same(primary, actual);
        Assert.Equal(1, first.Attempts);
        Assert.Equal(1, last.Attempts);
        if (storage == "ordinary")
        {
            var secondary = Assert.IsType<AggregateException>(primary.Data["TransportBufferCleanup"]);
            _ = Assert.IsType<InvalidOperationException>(Assert.Single(secondary.InnerExceptions));
        }

        owner.Dispose();
        Assert.Equal(1, first.Attempts);
        Assert.Equal(1, last.Attempts);
    }

    private static PrimaryFailure Primary(string storage) => storage switch
    {
        "ordinary" => new PrimaryFailure(new Hashtable()),
        "readOnly" => new PrimaryFailure(new ReadOnlyDictionary<object, object>(new Dictionary<object, object>())),
        "getter" => new PrimaryFailure(new Hashtable(), new InvalidOperationException("controlled Data getter failure")),
        "setter" => new PrimaryFailure(new DiagnosticDictionary(new InvalidOperationException("controlled Data setter failure"))),
        "oomGetter" => new PrimaryFailure(new Hashtable(), Activator.CreateInstance<OutOfMemoryException>()),
        "oomSetter" => new PrimaryFailure(new DiagnosticDictionary(Activator.CreateInstance<OutOfMemoryException>())),
        _ => throw new ArgumentOutOfRangeException(nameof(storage)),
    };
}