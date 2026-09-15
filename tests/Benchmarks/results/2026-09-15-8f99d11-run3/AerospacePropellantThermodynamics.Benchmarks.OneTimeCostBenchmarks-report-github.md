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
| LoadDatabase           |    10,836.09 μs |    303.383 μs |    349.376 μs |         - |         - |         - |   33429.7 KB |
| AssembleChemicalSystem |       456.03 μs |     37.689 μs |     41.891 μs |         - |         - |         - |    179.79 KB |
| UploadSpeciesTable     |        10.18 μs |      1.166 μs |      1.296 μs |         - |         - |         - |      2.82 KB |
| CompileCpuKernel       |   282,332.77 μs | 10,620.113 μs | 12,230.138 μs | 6000.0000 | 5000.0000 | 1000.0000 | 280214.31 KB |
| CompileCudaKernel      | 2,953,917.90 μs | 24,001.742 μs | 27,640.442 μs | 7000.0000 | 6000.0000 | 1000.0000 | 309937.79 KB |
