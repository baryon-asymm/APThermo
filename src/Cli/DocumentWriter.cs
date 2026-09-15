using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>Renders a document through a callback, delivers it to the output file or standard output, and decides the exit code.</summary>
internal static class DocumentWriter
{
    public static readonly JsonWriterOptions WriterOptions = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static ExitCode Write(RunInfo run, IReadOnlyList<CaseOutput> cases, CommandOptions options, TextWriter output)
    {
        var text = options.Format == OutputFormat.Csv ? CsvOutput.Render(cases) : JsonOutput.Render(run, cases);
        Deliver(text, options.Output, output);
        return ExitCodes.Of(cases);
    }

    public static void Deliver(string text, string? path, TextWriter output)
    {
        if (path is null)
        {
            output.Write(text);
            return;
        }

        try
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
        catch (DirectoryNotFoundException)
        {
            throw new InputException($"{path}: directory not found");
        }
    }

    public static string Render(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>The non-finite-number rule: a station value that is not finite is written as null.</summary>
    public static void WriteNumber(Utf8JsonWriter writer, string name, double value)
    {
        if (double.IsFinite(value))
        {
            writer.WriteNumber(name, value);
        }
        else
        {
            writer.WriteNull(name);
        }
    }
}
