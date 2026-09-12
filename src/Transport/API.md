# API.md — Transport

Namespace `AerospacePropellantThermodynamics.Transport`. The node exposes a compact
transport table for the species of a species table and a kernel-compatible
evaluation of the mixture transport properties at one station. Everything not listed
here is internal and may change.

## Transport table ⏳

```csharp
namespace AerospacePropellantThermodynamics.Transport;

public sealed class TransportTable                       // host side, immutable
{
    public static TransportTable Build(TransportDatabase database, SpeciesTable species);
    public IReadOnlyList<string> SpeciesWithoutData { get; }
    public TransportTableArrays Arrays { get; }
    public TransportTableView HostView { get; }
}

public readonly struct TransportTableView                // blittable
{
    public readonly int SpeciesCount;
    public readonly ArrayView<int> ViscosityStart, ViscosityCount;       // [species]
    public readonly ArrayView<int> ConductivityStart, ConductivityCount; // [species]
    public readonly ArrayView<double> Fits;                              // [fit * 6]: TLow, THigh, A, B, C, D
    public readonly ArrayView<int> PairIndex;                            // [species * SpeciesCount + species], −1 = no pair data
    public readonly ArrayView<int> PairViscosityStart, PairViscosityCount;
}
```

## Evaluation ⏳

```csharp
public struct TransportFigures                           // one station; SI
{
    public double Viscosity;                             // Pa·s
    public double FrozenConductivity;                    // W/(m·K)
    public double ReactingConductivity;                  // W/(m·K)
    public double FrozenPrandtl;
    public double ReactingPrandtl;
    public double ExcludedMoleFraction;                  // gaseous moles without transport data
}

public static class TransportSolver                      // kernel-compatible
{
    public static CaseStatus Evaluate(in SpeciesTableView species, in TransportTableView transport,
                                      in MixtureState state,
                                      ArrayView<double> moles,          // [species], kmol per kg
                                      ArrayView<double> multipliers,    // [element], from Equilibrium
                                      ArrayView<double> scratch,        // [ScratchDoubles(speciesCount)]
                                      out TransportFigures figures);
    public static int ScratchDoubles(int speciesCount);
}
```

## Errors

Never throws. Returns `Ok` or `NoTransportData` (excluded fraction above the node's
threshold); `figures` is fully written in both cases so that a caller may still report
the values with the status.

## Side effects

None.

## Out of scope

- Parsing `trans.inp`: `Data`.
- Deciding at which stations transport is evaluated: `Problems` and `Execution`.
