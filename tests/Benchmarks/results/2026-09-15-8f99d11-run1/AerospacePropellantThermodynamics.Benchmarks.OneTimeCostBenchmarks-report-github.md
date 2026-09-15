```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9445/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.112
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Toolchain=InProcessNoEmitToolchain  InvocationCount=1  IterationCount=20  
UnrollFactor=1  WarmupCount=5  

```
| Method                 | Mean            | Error         | StdDev        | Gen0      | Gen1      | Gen2      | Allocated    |
|----------------------- |----------------:|--------------:|--------------:|----------:|----------:|----------:|-------------:|
| LoadDatabase           |    11,073.40 μs |    241.846 μs |    278.511 μs |         - |         - |         - |   33429.7 KB |
| AssembleChemicalSystem |       473.64 μs |     57.636 μs |     66.374 μs |         - |         - |         - |    180.07 KB |
| UploadSpeciesTable     |        10.77 μs |      1.601 μs |      1.844 μs |         - |         - |         - |      2.87 KB |
| CompileCpuKernel       |   284,281.50 μs |  6,711.941 μs |  7,729.481 μs | 6000.0000 | 5000.0000 | 1000.0000 | 280222.91 KB |
| CompileCudaKernel      | 2,980,347.78 μs | 27,840.602 μs | 32,061.279 μs | 7000.0000 | 6000.0000 | 1000.0000 | 309988.37 KB |
