```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9445/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.112
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Toolchain=InProcessNoEmitToolchain  InvocationCount=1  IterationCount=20  
UnrollFactor=1  WarmupCount=5  

```
| Method     | CaseCount | Accelerator | Mean        | Error     | StdDev    | Allocated |
|----------- |---------- |------------ |------------:|----------:|----------:|----------:|
| **SolveBatch** | **1000**      | **Cpu**         |    **37.77 ms** |  **1.640 ms** |  **1.755 ms** |   **1.16 MB** |
| **SolveBatch** | **1000**      | **Cuda**        |    **16.16 ms** |  **0.621 ms** |  **0.715 ms** |   **1.17 MB** |
| **SolveBatch** | **10000**     | **Cpu**         |   **376.19 ms** |  **9.674 ms** | **10.753 ms** |  **11.57 MB** |
| **SolveBatch** | **10000**     | **Cuda**        |    **18.56 ms** |  **0.538 ms** |  **0.620 ms** |  **11.57 MB** |
| **SolveBatch** | **100000**    | **Cpu**         | **3,609.45 ms** | **36.129 ms** | **41.607 ms** |  **115.6 MB** |
| **SolveBatch** | **100000**    | **Cuda**        |   **123.33 ms** |  **1.204 ms** |  **1.387 ms** |  **115.6 MB** |
