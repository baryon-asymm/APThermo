```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9445/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.112
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Toolchain=InProcessNoEmitToolchain  InvocationCount=1  IterationCount=20  
UnrollFactor=1  WarmupCount=5  

```
| Method      | Accelerator | Selection | Mean       | Error     | StdDev    | Median     | Allocated |
|------------ |------------ |---------- |-----------:|----------:|----------:|-----------:|----------:|
| **SolveStates** | **Cpu**         | **All**       |  **23.583 ms** | **0.5501 ms** | **0.5886 ms** |  **23.670 ms** | **540.08 KB** |
| **SolveStates** | **Cpu**         | **Record1**   |   **5.871 ms** | **0.2800 ms** | **0.3112 ms** |   **5.747 ms** | **145.77 KB** |
| **SolveStates** | **Cpu**         | **Record2**   |   **5.684 ms** | **0.3717 ms** | **0.3817 ms** |   **5.694 ms** | **145.77 KB** |
| **SolveStates** | **Cpu**         | **Record3**   |   **3.039 ms** | **0.1875 ms** | **0.2084 ms** |   **3.099 ms** | **123.77 KB** |
| **SolveStates** | **Cpu**         | **Record4**   |  **10.548 ms** | **0.3678 ms** | **0.3935 ms** |  **10.552 ms** | **145.77 KB** |
| **SolveStates** | **Cuda**        | **All**       | **200.793 ms** | **2.0446 ms** | **2.3545 ms** | **202.546 ms** | **539.52 KB** |
| **SolveStates** | **Cuda**        | **Record1**   |  **84.058 ms** | **0.9272 ms** | **1.0678 ms** |  **84.653 ms** | **145.21 KB** |
| **SolveStates** | **Cuda**        | **Record2**   |  **84.285 ms** | **0.7637 ms** | **0.8795 ms** |  **84.648 ms** | **145.21 KB** |
| **SolveStates** | **Cuda**        | **Record3**   |  **43.159 ms** | **0.1126 ms** | **0.1297 ms** |  **43.177 ms** | **123.21 KB** |
| **SolveStates** | **Cuda**        | **Record4**   | **191.028 ms** | **1.8338 ms** | **2.1118 ms** | **191.976 ms** | **145.21 KB** |
