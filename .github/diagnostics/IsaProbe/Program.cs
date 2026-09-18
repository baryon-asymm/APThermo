// A CI diagnostic, not a tree node (IsaProbe.csproj). Prints the ISA facts .NET itself sees on the runner it executes
// on, so a hosted-runner bit-snapshot failure (the bits-diagnostics task) can be read back against the vector
// instruction sets .NET actually selected for that run, alongside the CPU model and core count the workflow's
// "Runner diagnostics" step prints without needing the SDK.
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

Console.WriteLine($"ProcessArchitecture: {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"OSDescription: {RuntimeInformation.OSDescription}");
Console.WriteLine($"Avx2.IsSupported: {Avx2.IsSupported}");
Console.WriteLine($"Fma.IsSupported: {Fma.IsSupported}");
Console.WriteLine($"Avx512F.IsSupported: {Avx512F.IsSupported}");
Console.WriteLine($"Environment.ProcessorCount: {Environment.ProcessorCount}");
