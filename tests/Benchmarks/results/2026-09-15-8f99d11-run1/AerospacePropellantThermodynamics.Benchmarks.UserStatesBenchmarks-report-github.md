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
| **SolveStates** | **Cpu**         | **All**       |  **23.130 ms** | **0.5652 ms** | **0.6282 ms** |  **550.8 KB** |
| **SolveStates** | **Cpu**         | **Record1**   |   **5.481 ms** | **0.3418 ms** | **0.3936 ms** |  **150.3 KB** |
| **SolveStates** | **Cpu**         | **Record2**   |   **5.653 ms** | **0.4324 ms** | **0.4806 ms** |  **150.3 KB** |
| **SolveStates** | **Cpu**         | **Record3**   |   **3.151 ms** | **0.2071 ms** | **0.2385 ms** |  **128.3 KB** |
| **SolveStates** | **Cpu**         | **Record4**   |   **9.919 ms** | **0.5140 ms** | **0.5919 ms** |  **150.3 KB** |
| **SolveStates** | **Cuda**        | **All**       | **186.703 ms** | **5.5509 ms** | **6.3925 ms** | **550.24 KB** |
| **SolveStates** | **Cuda**        | **Record1**   |  **83.103 ms** | **2.6290 ms** | **3.0276 ms** | **149.46 KB** |
| **SolveStates** | **Cuda**        | **Record2**   |  **81.894 ms** | **2.0184 ms** | **2.3244 ms** | **149.74 KB** |
| **SolveStates** | **Cuda**        | **Record3**   |  **41.756 ms** | **1.4000 ms** | **1.6123 ms** | **127.74 KB** |
| **SolveStates** | **Cuda**        | **Record4**   | **178.953 ms** | **4.4457 ms** | **5.1197 ms** | **149.74 KB** |
