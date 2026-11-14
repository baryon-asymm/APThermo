using System.Security.Cryptography;
using System.Text;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;

namespace APThermo.Execution.Tests;

/// <summary>Existing cached compiler metadata only; it does not measure physical register allocation or spills.</summary>
internal static class CachedKernelDiagnostics
{
    internal static IReadOnlyList<string> Lines(Delegate launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        if (!KernelUtil.TryGetKernel(launcher, out var kernel) || kernel is null)
        {
            return ["cached kernel metadata unavailable: the launcher exposes no kernel through ILGPU's public API"];
        }

        var lines = new List<string>
        {
            $"cached kernel compiler metadata: backend={kernel.AcceleratorType}; not a physical spill or occupancy measurement",
        };
        if (kernel.CompiledKernel is PTXCompiledKernel ptx)
        {
            lines.Add(PtxSummary(ptx.PTXAssembly));
        }
        else
        {
            lines.Add("cached PTX metadata unavailable: the compiled kernel is not PTX");
        }

        if (kernel.Info is { } info && !info.Functions.IsDefault)
        {
            lines.Add($"ILGPU FunctionInfo functions={info.Functions.Length}; LocalMemorySize describes compiler local allocations, not measured physical spills");
            foreach (var function in info.Functions)
            {
                if (function.LocalMemorySize > 0)
                {
                    lines.Add($"ILGPU FunctionInfo: name={function.Name}; localMemoryBytes={function.LocalMemorySize}");
                }
            }
        }
        else
        {
            lines.Add("ILGPU FunctionInfo unavailable under the existing compiler configuration; no option was enabled for diagnostics");
        }

        return lines;
    }

    internal static string PtxSummary(string ptx)
    {
        ArgumentNullException.ThrowIfNull(ptx);
        var bytes = Encoding.UTF8.GetBytes(ptx);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return $"cached PTX: utf8Bytes={bytes.Length}; sha256={sha256}; virtualRegisterDeclarationLines={RegisterDeclarationLines(ptx)}; declaration lines are not physical registers";
    }

    internal static int RegisterDeclarationLines(string ptx)
    {
        ArgumentNullException.ThrowIfNull(ptx);
        var count = 0;
        foreach (var line in ptx.AsSpan().EnumerateLines())
        {
            var text = line.TrimStart();
            if (text.StartsWith(".reg", StringComparison.Ordinal) && text.Length > 4 && char.IsWhiteSpace(text[4]))
            {
                count++;
            }
        }

        return count;
    }
}