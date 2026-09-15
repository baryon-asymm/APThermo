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
| **Solve**  | **False**     | **Tp**                   | **1,158.9 μs** | **142.57 μs** | **164.19 μs** |   **26.7 KB** |
| **Solve**  | **False**     | **Hp**                   |   **302.2 μs** |  **26.88 μs** |  **29.88 μs** |  **16.02 KB** |
| **Solve**  | **False**     | **Sp**                   |   **308.9 μs** |  **24.52 μs** |  **27.25 μs** |  **16.02 KB** |
| **Solve**  | **False**     | **Rocke(...)brium [25]** |   **532.9 μs** |  **56.68 μs** |  **60.65 μs** |  **22.34 KB** |
| **Solve**  | **False**     | **Rocke(...)amber [21]** |   **440.4 μs** |  **29.33 μs** |  **30.11 μs** |  **22.34 KB** |
| **Solve**  | **False**     | **RocketFrozenAtThroat** |   **446.5 μs** |  **29.89 μs** |  **31.99 μs** |   **22.4 KB** |
| **Solve**  | **True**      | **Tp**                   | **2,000.5 μs** | **151.97 μs** | **162.61 μs** |  **30.09 KB** |
| **Solve**  | **True**      | **Hp**                   |   **488.5 μs** |  **36.04 μs** |  **37.01 μs** |  **19.41 KB** |
| **Solve**  | **True**      | **Sp**                   |   **508.7 μs** |  **39.51 μs** |  **43.92 μs** |  **19.41 KB** |
| **Solve**  | **True**      | **Rocke(...)brium [25]** |   **736.1 μs** |  **54.13 μs** |  **60.16 μs** |  **25.84 KB** |
| **Solve**  | **True**      | **Rocke(...)amber [21]** |   **683.9 μs** |  **55.96 μs** |  **64.45 μs** |  **26.13 KB** |
| **Solve**  | **True**      | **RocketFrozenAtThroat** |   **749.9 μs** |  **82.35 μs** |  **94.83 μs** |  **26.13 KB** |
