namespace APThermo.Protocol.Tests;

/// <summary>A compiled generic record with both a direct base and an interface promise.</summary>
internal sealed record SignatureDerived<T>(T Item) : SignatureBase<T>(Item), IDisposable where T : class
{
    public void Dispose() => GC.KeepAlive(this);
}
