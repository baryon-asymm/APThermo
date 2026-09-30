using System.Diagnostics;
using System.Reflection;
using ILGPU;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution;

/// <summary>
/// The typed launchers of the entry points, compiled — and on CUDA post-linked — on first use and kept until <see cref="Clear"/> (the engine's dispose),
/// one entry per entry-point name. The only synchronised piece of an engine (API.md: an engine is used from one thread at a time).
/// </summary>
internal sealed class KernelCache(AcceleratorSession session)
{
    private readonly Dictionary<string, Delegate> _launchers = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>How many launchers the cache holds: zero for a new cache and after <see cref="Clear"/>.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _launchers.Count;
            }
        }
    }

    /// <summary>
    /// Releases everything this cache and its context hold of the compiled kernels (2026-09-30, "Release at dispose"): every
    /// launcher, and the ILGPU context's caches of the intermediate representation the compile built, which are what stays
    /// reachable through the disposed session (143 MiB after one rocket kernel, 3.9 GB before the inlining bound; dropping
    /// the launchers alone frees none of it). The engine calls it before it disposes its session, so a disposed engine that
    /// stays reachable — a field, a static fixture — holds no compiled kernel. A later <see cref="Get{TDelegate}"/> compiles
    /// again; the engine never asks after its own disposal. The context call touches no accelerator cache.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _launchers.Clear();
            session.Context.ClearCache(ClearCacheMode.Everything);
        }
    }

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
            var launcher = Load(session, name).CreateLauncherDelegate<TDelegate>();
            _launchers[name] = launcher;
            warmUp = watch.Elapsed;
            return launcher;
        }
    }

    /// <summary>
    /// Compiles — and on CUDA post-links — the named entry point and loads it on the session's accelerator: the one load path
    /// every kernel of this node goes through, the bind-time probe (<see cref="AcceleratorChoice"/>) included, so that no
    /// caller has a second copy of the compile-post-link-load sequence.
    /// </summary>
    internal static Kernel Load(AcceleratorSession session, string name)
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
        return cuda.LoadAutoGroupedKernel(LibDevicePostLink.Link(cuda, session.Nvvm!, compiled).Kernel);
    }
}
