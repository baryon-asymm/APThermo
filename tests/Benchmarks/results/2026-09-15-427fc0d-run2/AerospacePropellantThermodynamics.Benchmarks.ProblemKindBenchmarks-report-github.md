```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9445/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.112
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Toolchain=InProcessNoEmitToolchain  InvocationCount=1  IterationCount=20  
UnrollFactor=1  WarmupCount=5  

```
| Method | Transport | Kind                 | Mean       | Error     | StdDev    | Allocated |
|------- |---------- |--------------------- |-----------:|----------:|----------:|----------:|
| **Solve**  | **False**     | **Tp**                   | **1,333.6 μs** | **120.83 μs** | **129.28 μs** |   **24.2 KB** |
| **Solve**  | **False**     | **Hp**                   |   **279.3 μs** |  **30.34 μs** |  **33.72 μs** |  **13.51 KB** |
| **Solve**  | **False**     | **Sp**                   |   **269.4 μs** |  **20.21 μs** |  **22.46 μs** |  **13.51 KB** |
| **Solve**  | **False**     | **Rocke(...)brium [25]** |   **497.8 μs** |  **65.37 μs** |  **72.66 μs** |  **18.69 KB** |
| **Solve**  | **False**     | **Rocke(...)amber [21]** |   **423.0 μs** |  **29.92 μs** |  **34.46 μs** |  **18.69 KB** |
| **Solve**  | **False**     | **RocketFrozenAtThroat** |   **443.8 μs** |  **63.46 μs** |  **73.08 μs** |  **18.69 KB** |
| **Solve**  | **True**      | **Tp**                   | **2,506.3 μs** | **164.72 μs** | **176.24 μs** |  **26.72 KB** |
| **Solve**  | **True**      | **Hp**                   |   **464.9 μs** |  **41.89 μs** |  **44.82 μs** |  **16.03 KB** |
| **Solve**  | **True**      | **Sp**                   |   **467.1 μs** |  **44.05 μs** |  **50.72 μs** |  **16.03 KB** |
| **Solve**  | **True**      | **Rocke(...)brium [25]** |   **692.1 μs** |  **49.80 μs** |  **57.35 μs** |   **21.6 KB** |
| **Solve**  | **True**      | **Rocke(...)amber [21]** |   **673.5 μs** |  **66.27 μs** |  **76.32 μs** |   **21.6 KB** |
| **Solve**  | **True**      | **RocketFrozenAtThroat** |   **691.4 μs** |  **51.20 μs** |  **54.79 μs** |   **21.6 KB** |
