```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9445/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.112
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Toolchain=InProcessNoEmitToolchain  InvocationCount=1  IterationCount=20  
UnrollFactor=1  WarmupCount=5  

```
| Method      | Accelerator | Selection | Mean       | Error     | StdDev    | Allocated |
|------------ |------------ |---------- |-----------:|----------:|----------:|----------:|
| **SolveStates** | **Cpu**         | **All**       |  **22.214 ms** | **0.4945 ms** | **0.5496 ms** |  **550.8 KB** |
| **SolveStates** | **Cpu**         | **Record1**   |   **5.582 ms** | **0.2687 ms** | **0.3094 ms** |  **150.3 KB** |
| **SolveStates** | **Cpu**         | **Record2**   |   **5.502 ms** | **0.3336 ms** | **0.3842 ms** |  **150.3 KB** |
| **SolveStates** | **Cpu**         | **Record3**   |   **2.995 ms** | **0.2608 ms** | **0.3003 ms** |  **128.3 KB** |
| **SolveStates** | **Cpu**         | **Record4**   |   **9.772 ms** | **0.5925 ms** | **0.6339 ms** |  **150.3 KB** |
| **SolveStates** | **Cuda**        | **All**       | **176.332 ms** | **1.5630 ms** | **1.7999 ms** | **550.24 KB** |
| **SolveStates** | **Cuda**        | **Record1**   |  **78.692 ms** | **0.7312 ms** | **0.8420 ms** | **149.46 KB** |
| **SolveStates** | **Cuda**        | **Record2**   |  **76.763 ms** | **0.7291 ms** | **0.8397 ms** | **149.74 KB** |
| **SolveStates** | **Cuda**        | **Record3**   |  **40.278 ms** | **0.1068 ms** | **0.1230 ms** | **127.74 KB** |
| **SolveStates** | **Cuda**        | **Record4**   | **167.090 ms** | **1.7166 ms** | **1.9768 ms** | **149.74 KB** |
