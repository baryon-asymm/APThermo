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
| LoadDatabase           |    10,796.26 μs |    221.343 μs |    246.022 μs |         - |         - |         - |  33480.34 KB |
| AssembleChemicalSystem |       536.56 μs |      9.678 μs |      9.939 μs |         - |         - |         - |    515.32 KB |
| UploadSpeciesTable     |        10.82 μs |      1.326 μs |      1.474 μs |         - |         - |         - |      2.82 KB |
| CompileCpuKernel       |   170,326.71 μs | 10,296.623 μs | 11,857.607 μs | 4000.0000 | 4000.0000 | 2000.0000 | 180274.67 KB |
| CompileCudaKernel      | 2,973,645.80 μs | 49,035.450 μs | 56,469.297 μs | 5000.0000 | 4000.0000 | 2000.0000 | 209272.85 KB |
