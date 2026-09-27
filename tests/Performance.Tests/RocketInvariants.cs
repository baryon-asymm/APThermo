using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>
/// The node's invariants (Performance BOOT.md) as predicates over one host solve: each returns the stations that violate it as
/// messages, empty when the invariant holds everywhere it applies. One method per invariant, so a test names one fact.
/// </summary>
internal static class RocketInvariants
{
    /// <summary>Equation (6.16): the throat's u²/a² departs from 1 by no more than this (Performance BOOT.md, Invariants: sonic throat).</summary>
    public const double SonicTolerance = 4e-5;

    /// <summary>Every station downstream of the chamber keeps the chamber's entropy to this relative tolerance (Performance BOOT.md, Invariants: isentropic expansion).</summary>
    public const double EntropyTolerance = 1e-9;

    /// <summary>The assigned area ratio is met at convergence to this relative tolerance (Performance BOOT.md, Invariants: area ratios are met by construction).</summary>
    public const double AreaRatioTolerance = 1e-6;

    /// <summary>u = sqrt(2(h_c − h)) is an algebraic identity of a converged station: rounding only.</summary>
    public const double VelocityTolerance = 1e-9;

    /// <summary>p_c/p at an assigned pressure-ratio station is an algebraic identity of a converged station: rounding only.</summary>
    public const double PressureRatioTolerance = 1e-12;

    /// <summary>Equation (6.16): the throat is sonic.</summary>
    public static IReadOnlyList<string> SonicThroat(RocketSolution solution)
    {
        var throat = solution.Outcome.Stations[RocketSolver.Throat];
        var sonic = throat.Velocity * throat.Velocity / (throat.SoundSpeed * throat.SoundSpeed);
        return Math.Abs(sonic - 1.0) > SonicTolerance ? [$"throat u²/a² = {sonic:R}"] : [];
    }

    /// <summary>Every station downstream of the chamber carries the chamber's entropy.</summary>
    public static IReadOnlyList<string> ConstantEntropy(RocketSolution solution)
    {
        var chamber = solution.Outcome.Stations[RocketSolver.Chamber];
        var violations = new List<string>();
        for (var s = 1; s < solution.StationCount; s++)
        {
            var entropy = solution.Outcome.Stations[s].Entropy;
            if (Math.Abs(entropy - chamber.Entropy) > EntropyTolerance * Math.Abs(chamber.Entropy))
            {
                violations.Add($"station {s} entropy {entropy:R} against the chamber's {chamber.Entropy:R}");
            }
        }

        return violations;
    }

    /// <summary>The energy equation of section 6.2: u² = 2(h_c − h).</summary>
    public static IReadOnlyList<string> EnergyEquation(RocketSolution solution)
    {
        var chamber = solution.Outcome.Stations[RocketSolver.Chamber];
        var violations = new List<string>();
        for (var s = 1; s < solution.StationCount; s++)
        {
            var state = solution.Outcome.Stations[s];
            var expected = Math.Sqrt(2.0 * (chamber.Enthalpy - state.Enthalpy));
            if (Math.Abs(state.Velocity - expected) > VelocityTolerance * expected)
            {
                violations.Add($"station {s} velocity {state.Velocity:R} against the energy equation's {expected:R}");
            }
        }

        return violations;
    }

    /// <summary>Every exit station reproduces the area ratio or the pressure ratio it was assigned.</summary>
    public static IReadOnlyList<string> AssignedExit(RocketSolution solution)
    {
        var chamber = solution.Outcome.Stations[RocketSolver.Chamber];
        var throat = solution.Outcome.Stations[RocketSolver.Throat];
        var massFluxThroat = throat.Density * throat.Velocity;
        var inputs = solution.Inputs;
        var violations = new List<string>();
        for (var k = 0; k < inputs.ExitCount; k++)
        {
            var message = ExitMismatch(solution, chamber, massFluxThroat, inputs, k);
            if (message is not null)
            {
                violations.Add(message);
            }
        }

        return violations;
    }

    /// <summary>Every accepted area-ratio exit station is supersonic (Performance BOOT.md, 2026-09-26, finding F3).</summary>
    public static IReadOnlyList<string> SupersonicAreaRatioExits(RocketSolution solution)
    {
        var inputs = solution.Inputs;
        var violations = new List<string>();
        for (var k = 0; k < inputs.ExitCount; k++)
        {
            var station = RocketLayout.FixedStations + k;
            if (inputs.Exits.Kinds[k] != ExitSpecification.AreaRatio || solution.Outcome.StationStatus[station] != CaseStatus.Ok)
            {
                continue;
            }

            var mach = solution.Outcome.Stations[station].Mach;
            if (!(mach >= 1.0))
            {
                violations.Add($"station {station} Mach {mach:R} is not supersonic");
            }
        }

        return violations;
    }

    /// <summary>
    /// Every station's <c>PressureRatio</c> is p_c/p of the state actually solved there, to rounding level (Performance
    /// BOOT.md, 2026-09-26, finding F6: the throat's figure must come from the pressure actually solved, not one
    /// momentum step past it).
    /// </summary>
    public static IReadOnlyList<string> PressureRatioMatchesTheSolvedPressure(RocketSolution solution)
    {
        var chamberPressure = solution.Outcome.Stations[RocketSolver.Chamber].Pressure;
        var violations = new List<string>();
        for (var s = 1; s < solution.StationCount; s++)
        {
            if (solution.Outcome.StationStatus[s] != CaseStatus.Ok)
            {
                continue;
            }

            var expected = chamberPressure / solution.Outcome.Stations[s].Pressure;
            var actual = solution.Outcome.Figures[s].PressureRatio;
            if (BitConverter.DoubleToInt64Bits(expected) != BitConverter.DoubleToInt64Bits(actual))
            {
                violations.Add($"station {s} PressureRatio {actual:R} against p_c/p {expected:R}");
            }
        }

        return violations;
    }

    /// <summary>The composition downstream of the freezing station is bit-identical to it.</summary>
    public static IReadOnlyList<string> FrozenComposition(RocketSolution solution)
    {
        var freezingStation = FreezingStationOf(solution.Inputs.Flow);
        if (freezingStation < 0)
        {
            return [];
        }

        var violations = new List<string>();
        for (var s = freezingStation + 1; s < solution.StationCount; s++)
        {
            var message = CompositionMismatch(solution, freezingStation, s);
            if (message is not null)
            {
                violations.Add(message);
            }
        }

        return violations;
    }

    private static string? ExitMismatch(RocketSolution solution, MixtureState chamber, double massFluxThroat, RocketInputs inputs, int k)
    {
        var s = RocketLayout.FixedStations + k;
        var state = solution.Outcome.Stations[s];
        if (inputs.Exits.Kinds[k] == ExitSpecification.AreaRatio)
        {
            var areaRatio = massFluxThroat / (state.Density * state.Velocity);
            return Math.Abs(areaRatio - inputs.Exits.Values[k]) > AreaRatioTolerance * inputs.Exits.Values[k]
                ? $"station {s} area ratio {areaRatio:R} against the assigned {inputs.Exits.Values[k]:R}"
                : null;
        }

        var pressureRatio = chamber.Pressure / state.Pressure;
        return Math.Abs(pressureRatio - inputs.Exits.Values[k]) > PressureRatioTolerance * inputs.Exits.Values[k]
            ? $"station {s} pressure ratio {pressureRatio:R} against the assigned {inputs.Exits.Values[k]:R}"
            : null;
    }

    private static int FreezingStationOf(FlowModel flow) => flow switch
    {
        FlowModel.FrozenAtChamber => RocketSolver.Chamber,
        FlowModel.FrozenAtThroat => RocketSolver.Throat,
        FlowModel.ShiftingEquilibrium => -1,
        _ => -1,
    };

    private static string? CompositionMismatch(RocketSolution solution, int freezingStation, int station)
    {
        var speciesCount = solution.Table.SpeciesCount;
        for (var j = 0; j < speciesCount; j++)
        {
            var frozenMoles = solution.Outcome.Moles[freezingStation * speciesCount + j];
            var stationMoles = solution.Outcome.Moles[station * speciesCount + j];
            if (BitConverter.DoubleToInt64Bits(frozenMoles) != BitConverter.DoubleToInt64Bits(stationMoles))
            {
                return $"station {station} moles of {solution.Table.Species[j]} differ from the freezing station's";
            }
        }

        return null;
    }
}
