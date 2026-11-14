namespace APThermo.Protocol.Tests;

/// <summary>A compiled interface whose inherited generic and nongeneric contracts can be documented selectively.</summary>
internal interface ISignatureSequence<T> : IEnumerable<T>
{
    int Length { get; }
}
