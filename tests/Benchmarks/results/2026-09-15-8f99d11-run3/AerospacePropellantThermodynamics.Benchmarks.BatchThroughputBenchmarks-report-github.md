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
| **SolveBatch** | **1000**      | **Cpu**         |    **35.87 ms** |  **1.772 ms** |  **2.040 ms** |   **1.16 MB** |
| **SolveBatch** | **1000**      | **Cuda**        |    **15.60 ms** |  **0.223 ms** |  **0.257 ms** |   **1.17 MB** |
| **SolveBatch** | **10000**     | **Cpu**         |   **335.64 ms** |  **5.376 ms** |  **5.752 ms** |  **11.57 MB** |
| **SolveBatch** | **10000**     | **Cuda**        |    **17.77 ms** |  **0.231 ms** |  **0.266 ms** |  **11.57 MB** |
| **SolveBatch** | **100000**    | **Cpu**         | **3,362.50 ms** | **15.560 ms** | **17.919 ms** | **115.61 MB** |
| **SolveBatch** | **100000**    | **Cuda**        |   **130.82 ms** |  **3.858 ms** |  **4.128 ms** |  **115.6 MB** |
