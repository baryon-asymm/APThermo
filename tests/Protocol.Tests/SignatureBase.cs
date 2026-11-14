namespace APThermo.Protocol.Tests;

/// <summary>A real generic record base for positive and negative documented-inheritance controls.</summary>
internal abstract record SignatureBase<T>(T Value) where T : class;
