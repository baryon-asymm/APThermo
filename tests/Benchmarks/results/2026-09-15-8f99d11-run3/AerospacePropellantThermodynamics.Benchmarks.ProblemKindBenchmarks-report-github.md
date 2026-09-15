```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9445/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.112
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Toolchain=InProcessNoEmitToolchain  InvocationCount=1  IterationCount=20  
UnrollFactor=1  WarmupCount=5  

```
| Method | Transport | Kind                 | Mean       | Error    | StdDev    | Allocated |
|------- |---------- |--------------------- |-----------:|---------:|----------:|----------:|
| **Solve**  | **False**     | **Tp**                   | **1,089.0 μs** | **99.90 μs** | **102.59 μs** |   **26.7 KB** |
| **Solve**  | **False**     | **Hp**                   |   **281.3 μs** | **17.27 μs** |  **17.73 μs** |  **16.02 KB** |
| **Solve**  | **False**     | **Sp**                   |   **300.0 μs** | **23.47 μs** |  **26.09 μs** |  **16.02 KB** |
| **Solve**  | **False**     | **Rocke(...)brium [25]** |   **519.6 μs** | **50.65 μs** |  **54.19 μs** |  **22.34 KB** |
| **Solve**  | **False**     | **Rocke(...)amber [21]** |   **443.4 μs** | **47.33 μs** |  **54.50 μs** |  **22.34 KB** |
| **Solve**  | **False**     | **RocketFrozenAtThroat** |   **454.8 μs** | **30.51 μs** |  **32.65 μs** |   **22.4 KB** |
| **Solve**  | **True**      | **Tp**                   | **2,013.3 μs** | **94.58 μs** | **101.20 μs** |  **30.09 KB** |
| **Solve**  | **True**      | **Hp**                   |   **514.8 μs** | **66.37 μs** |  **76.43 μs** |  **19.41 KB** |
| **Solve**  | **True**      | **Sp**                   |   **520.2 μs** | **51.33 μs** |  **59.11 μs** |  **19.41 KB** |
| **Solve**  | **True**      | **Rocke(...)brium [25]** |   **723.9 μs** | **46.81 μs** |  **53.91 μs** |  **26.13 KB** |
| **Solve**  | **True**      | **Rocke(...)amber [21]** |   **664.5 μs** | **43.34 μs** |  **46.37 μs** |  **26.13 KB** |
| **Solve**  | **True**      | **RocketFrozenAtThroat** |   **680.7 μs** | **61.26 μs** |  **70.55 μs** |  **25.84 KB** |
