using System.Diagnostics;
using System.Reflection;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution;

/// <summary>
/// The typed launchers of the entry points, compiled — and on CUDA post-linked — on first use and kept for the session's lifetime,
/// one entry per entry-point name. The only synchronised piece of an engine (API.md: an engine is used from one thread at a time).
/// </summary>
internal sealed class KernelCache(AcceleratorSession session)
{
    private readonly Dictionary<string, Delegate> _launchers = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>The launcher of the named entry point; <paramref name="warmUp"/> is the compilation time, zero when it was cached.</summary>
    public TDelegate Get<TDelegate>(string name, out TimeSpan warmUp) where TDelegate : Delegate
    {
        lock (_gate)
        {
            warmUp = TimeSpan.Zero;
            if (_launchers.TryGetValue(name, out var cached))
            {
                return (TDelegate)cached;
            }

            var watch = Stopwatch.StartNew();
            var launcher = Load(name).CreateLauncherDelegate<TDelegate>();
            _launchers[name] = launcher;
            warmUp = watch.Elapsed;
            return launcher;
        }
    }

    private Kernel Load(string name)
    {
        var method = typeof(Kernels).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
                     ?? throw new InvalidOperationException($"no kernel named {name}");
        if (session.Accelerator is not CudaAccelerator cuda)
        {
            return session.Accelerator.LoadAutoGroupedKernel(method);
        }

        // Every CUDA kernel goes through the post-link; the CPU accelerator loads the method as ILGPU does.
        var entry = EntryPointDescription.FromImplicitlyGroupedKernel(method);
        var compiled = (PTXCompiledKernel)cuda.Backend.Compile(entry, KernelSpecialization.Empty);
        return cuda.LoadAutoGroupedKernel(LibDevicePostLink.Link(cuda, session.Nvvm!, compiled));
    }
}
