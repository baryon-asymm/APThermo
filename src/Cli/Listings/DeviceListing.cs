using System.Text.Json;
using APThermo.Cli.Output;
using APThermo.Cli.Syntax;
using APThermo.Execution;

namespace APThermo.Cli.Listings;

/// <summary>The devices command: its rendering of a <see cref="DeviceReport"/>.</summary>
internal static class DeviceListing
{
    public static ExitCode Execute(Invocation invocation, TextWriter output)
    {
        var report = DeviceProbe.Run();
        var text = DocumentWriter.Render(writer => Write(writer, report));
        DocumentWriter.Deliver(text, invocation.Options.Output, output);
        return ExitCode.Ok;
    }

    private static void Write(Utf8JsonWriter writer, DeviceReport report)
    {
        writer.WriteStartObject();
        writer.WriteStartObject("run");
        writer.WriteString("tool", Program.ToolName);
        writer.WriteString("version", Program.Version);
        writer.WriteString("command", "devices");
        writer.WriteEndObject();
        writer.WriteBoolean("cudaForbidden", Engine.CudaForbidden);
        writer.WritePropertyName("cpu");
        RunSection.WriteAccelerator(writer, report.Cpu);
        writer.WritePropertyName("cuda");
        WriteCuda(writer, report);
        writer.WriteEndObject();
    }

    private static void WriteCuda(Utf8JsonWriter writer, DeviceReport report)
    {
        if (report.Cuda is { } cuda)
        {
            RunSection.WriteAccelerator(writer, cuda);
            return;
        }

        writer.WriteStartObject();
        writer.WriteBoolean("available", false);
        writer.WriteString("message", report.CudaMessage);
        writer.WriteStartArray("pathsTried");
        foreach (var path in report.CudaPathsTried)
        {
            writer.WriteStringValue(path);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
