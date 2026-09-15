using APThermo.Execution;

namespace APThermo.Cli.Listings;

/// <summary>What the machine offers, asked once: a CPU accelerator always, a CUDA one tried and its failure kept if it does not bind.</summary>
internal static class DeviceProbe
{
    public static DeviceReport Run()
    {
        var cpu = AcceleratorProbe.Describe(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        try
        {
            var cuda = AcceleratorProbe.Describe(new EngineOptions { Accelerator = AcceleratorKind.Cuda });
            return new DeviceReport(cpu, cuda, null, []);
        }
        catch (AcceleratorUnavailableException e)
        {
            return new DeviceReport(cpu, null, e.Message, e.PathsTried);
        }
        catch (InvalidOperationException e)
        {
            return new DeviceReport(cpu, null, e.Message, []);
        }
    }
}
