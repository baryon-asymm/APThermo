using APThermo.Execution;

namespace APThermo.Cli.Listings;

/// <summary>What the machine offers, asked once: a CPU engine always, a CUDA one tried and its failure kept if it does not bind.</summary>
internal static class DeviceProbe
{
    public static DeviceReport Run()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        try
        {
            using var cuda = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cuda });
            return new DeviceReport(cpu.Accelerator, cuda.Accelerator, null, []);
        }
        catch (AcceleratorUnavailableException e)
        {
            return new DeviceReport(cpu.Accelerator, null, e.Message, e.PathsTried);
        }
        catch (InvalidOperationException e)
        {
            return new DeviceReport(cpu.Accelerator, null, e.Message, []);
        }
    }
}
