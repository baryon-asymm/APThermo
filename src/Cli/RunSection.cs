using System.Text.Json;
using AerospacePropellantThermodynamics.Execution;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The `run` object and the accelerator object, for every command that writes them.</summary>
internal static class RunSection
{
    public static void Write(Utf8JsonWriter writer, RunInfo run)
    {
        writer.WriteStartObject("run");
        writer.WriteString("tool", Program.ToolName);
        writer.WriteString("version", Program.Version);
        writer.WriteString("command", run.Command);
        writer.WriteStartArray("inputs");
        foreach (var input in run.Inputs)
        {
            writer.WriteStringValue(input);
        }

        writer.WriteEndArray();
        if (run.Database is { } database)
        {
            writer.WriteStartObject("database");
            writer.WriteString("thermoPath", database.ThermoPath);
            writer.WriteString("transPath", database.TransPath);
            writer.WriteString("thermoSha256", database.ThermoSha256);
            writer.WriteString("transSha256", database.TransSha256);
            writer.WriteEndObject();
        }

        if (run.Accelerator is { } accelerator)
        {
            writer.WritePropertyName("accelerator");
            WriteAccelerator(writer, accelerator);
        }

        writer.WriteStartObject("timings");
        writer.WriteNumber("database", run.Timings.Database);
        writer.WriteNumber("solve", run.Timings.Solve);
        writer.WriteEndObject();
        writer.WriteNumber("threshold", run.Limits.Threshold);
        writer.WriteNumber("massTolerance", run.Limits.MassTolerance);
        writer.WriteEndObject();
    }

    public static void WriteAccelerator(Utf8JsonWriter writer, AcceleratorInfo accelerator)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Names.Accelerator(accelerator.Kind));
        writer.WriteString("deviceName", accelerator.DeviceName);
        writer.WriteString("ilgpuVersion", accelerator.IlgpuVersion);
        writer.WriteString("libNvvmPath", accelerator.LibNvvmPath);
        writer.WriteString("libDevicePath", accelerator.LibDevicePath);
        writer.WriteNumber("threadsOrMultiprocessors", accelerator.ThreadsOrMultiprocessors);
        writer.WriteEndObject();
    }
}
