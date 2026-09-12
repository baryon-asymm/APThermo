using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>One station of a rocket result, or the state of an equilibrium result; compositions by name, without a threshold.</summary>
public sealed record Station(
    string Name,                                        // "chamber", "throat", "exit1", "exit2", … ; "state" for an equilibrium result
    MixtureState State,                                 // zero where the status is not Ok
    PerformanceFigures? Performance,                    // rocket stations only
    IReadOnlyDictionary<string, double> MoleFractions,  // every species of the table, n_j over the moles of all species, as the reference reports them
    IReadOnlyDictionary<string, double> CondensedMassFractions,  // every condensed species of the table, n_j M_j
    TransportFigures? Transport,                        // null when not requested or not Ok; see TransportStatus
    CaseStatus? TransportStatus,                        // null when transport was not requested
    CaseStatus Status);

/// <summary>What one rocket case produced.</summary>
public sealed record RocketResult(
    Propellant? Propellant,                             // null for an elemental mixture
    ElementalMixture Mixture,                           // the element moles and enthalpy the case started from
    RocketProblem Problem,
    double? OxidizerToFuelRatio,                        // the ratio of the mixture rule, or null
    IReadOnlyList<string> Species,                      // table order: gases, then condensed species
    IReadOnlyList<Station> Stations,                    // chamber, throat, exits in order
    CaseStatus Status,
    AcceleratorInfo Accelerator);

/// <summary>What one equilibrium case produced.</summary>
public sealed record EquilibriumResult(
    Propellant? Propellant,
    ElementalMixture Mixture,
    EquilibriumProblem Problem,
    IReadOnlyList<string> Species,
    Station State,
    CaseStatus Status,
    AcceleratorInfo Accelerator);
