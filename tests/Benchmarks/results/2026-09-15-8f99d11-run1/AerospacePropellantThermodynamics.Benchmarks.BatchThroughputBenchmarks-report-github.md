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
| **SolveBatch** | **1000**      | **Cpu**         |    **36.25 ms** |  **1.587 ms** |  **1.698 ms** |   **1.16 MB** |
| **SolveBatch** | **1000**      | **Cuda**        |    **16.07 ms** |  **0.509 ms** |  **0.586 ms** |   **1.17 MB** |
| **SolveBatch** | **10000**     | **Cpu**         |   **351.30 ms** | **11.466 ms** | **13.205 ms** |  **11.57 MB** |
| **SolveBatch** | **10000**     | **Cuda**        |    **18.60 ms** |  **0.528 ms** |  **0.587 ms** |  **11.57 MB** |
| **SolveBatch** | **100000**    | **Cpu**         | **3,470.44 ms** | **28.806 ms** | **33.173 ms** | **115.61 MB** |
| **SolveBatch** | **100000**    | **Cuda**        |   **139.45 ms** |  **4.575 ms** |  **5.268 ms** |  **115.6 MB** |
