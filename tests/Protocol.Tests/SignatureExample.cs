namespace APThermo.Protocol.Tests;

/// <summary>Synthetic compiled signatures for semantic guard controls; no production API is added.</summary>
internal sealed class SignatureExample<T> where T : class
{
    public static (CancellationToken Token, string? Text, int? Number) DefaultArguments(
        string? text = null, int? number = null, CancellationToken token = default) => (token, text, number);

    public const int Limit = 7;

    public T? Value { get; private set; }

    public event Action? Changed;

    public TResult Convert<TResult>(T value, out bool success, params int[] counts) where TResult : struct
    {
        Value = value;
        Changed?.Invoke();
        success = counts.Length > 0;
        return default;
    }
}
