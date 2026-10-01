# API.md — .github/diagnostics/IsaProbe

The node exposes a command line and no C# surface: its only program is the top-level
statements of `Program.cs`, and it declares no public type.

## Command line ✅

Run directly, with its project argument pointed at this directory (it is not in
`APThermo.sln`): `dotnet run --project .github/diagnostics/IsaProbe --configuration Release`.

| Output line | Meaning |
|---|---|
| `ProcessArchitecture` | `RuntimeInformation.ProcessArchitecture` |
| `OSDescription` | `RuntimeInformation.OSDescription` |
| `Avx2.IsSupported` | whether .NET selected the AVX2 instruction set |
| `Fma.IsSupported` | whether .NET selected the FMA instruction set |
| `Avx512F.IsSupported` | whether .NET selected the AVX-512 foundation instruction set |
| `Environment.ProcessorCount` | the logical core count .NET sees |

Each line is `<name>: <value>` on the standard output, in the order above. The program
returns no exit code of its own other than 0. The caller is the runner-diagnostics action of [`.github`](../../API.md).
