namespace APThermo.Cli;

/// <summary>What the user gave cannot be used: an option, a document, a path; exit code 2.</summary>
internal sealed class InputException : Exception
{
    /// <summary>Initializes a new instance with no message.</summary>
    public InputException()
    {
    }

    /// <summary>Initializes a new instance with the given message.</summary>
    public InputException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance with the given message and inner exception.</summary>
    public InputException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
