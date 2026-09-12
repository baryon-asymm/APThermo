using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance.Tests;

/// <summary>L0: the node's invariants on every converged fixture case, and the statuses of invalid exits.</summary>
[Collection(CpuCollection.Name)]
public sealed class InvariantTests(CpuFixture fixture)
{
    /// <summary>The invariants' tolerances (Performance BOOT.md).</summary>
    private const double EntropyTolerance = 1e-9;
    private const double SonicTolerance = 4e-5;
    private const double AreaRatioTolerance = 1e-6;

    public static IEnumerable<object[]> Cases() => RocketHost.Cases();

    [Theory]
    [MemberData(nameof(Cases))]
    public void Entropy_sonic_throat_area_ratio_and_frozen_composition_hold(string name)
    {
        var c = RocketHost.Load(name);
        var inputs = RocketInputs.Of(c);
        var solution = RocketHost.Solve(fixture, inputs);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        var violations = new List<string>();
        var chamber = solution.Stations[0];
        var throat = solution.Stations[1];
        var massFluxThroat = throat.Density * throat.Velocity;
        var sonic = throat.Velocity * throat.Velocity / (throat.SoundSpeed * throat.SoundSpeed);
        if (Math.Abs(sonic - 1.0) > SonicTolerance)
        {
            violations.Add($"throat u²/a² = {sonic:R}");
        }

        var freezingStation = inputs.Flow switch
        {
            FlowModel.FrozenAtChamber => 0,
            FlowModel.FrozenAtThroat => 1,
            _ => -1,
        };
        var speciesCount = solution.Table.SpeciesCount;
        for (var s = 1; s < solution.StationCount; s++)
        {
            var state = solution.Stations[s];
            if (Math.Abs(state.Entropy - chamber.Entropy) > EntropyTolerance * Math.Abs(chamber.Entropy))
            {
                violations.Add($"station {s} entropy {state.Entropy:R} against the chamber's {chamber.Entropy:R}");
            }

            var expectedVelocity = Math.Sqrt(2.0 * (chamber.Enthalpy - state.Enthalpy));
            if (Math.Abs(state.Velocity - expectedVelocity) > 1e-9 * expectedVelocity)
            {
                violations.Add($"station {s} velocity {state.Velocity:R} against the energy equation's {expectedVelocity:R}");
            }

            if (s >= RocketLayout.FixedStations)
            {
                var k = s - RocketLayout.FixedStations;
                var areaRatio = massFluxThroat / (state.Density * state.Velocity);
                if (inputs.ExitKinds[k] == ExitSpecification.AreaRatio && Math.Abs(areaRatio - inputs.ExitValues[k]) > AreaRatioTolerance * inputs.ExitValues[k])
                {
                    violations.Add($"station {s} area ratio {areaRatio:R} against the assigned {inputs.ExitValues[k]:R}");
                }

                if (inputs.ExitKinds[k] == ExitSpecification.PressureRatio && Math.Abs(chamber.Pressure / state.Pressure - inputs.ExitValues[k]) > 1e-12 * inputs.ExitValues[k])
                {
                    violations.Add($"station {s} pressure ratio {chamber.Pressure / state.Pressure:R} against the assigned {inputs.ExitValues[k]:R}");
                }
            }

            if (freezingStation >= 0 && s > freezingStation)
            {
                for (var j = 0; j < speciesCount; j++)
                {
                    var frozenMoles = solution.Moles[freezingStation * speciesCount + j];
                    var stationMoles = solution.Moles[s * speciesCount + j];
                    if (BitConverter.DoubleToInt64Bits(frozenMoles) != BitConverter.DoubleToInt64Bits(stationMoles))
                    {
                        violations.Add($"station {s} moles of {solution.Table.Species[j]} differ from the freezing station's");
                        break;
                    }
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    [Fact]
    public void An_area_ratio_below_one_fails_its_station_only()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var mutated = inputs with { ExitValues = [0.5, inputs.ExitValues[0]], ExitKinds = [ExitSpecification.AreaRatio, ExitSpecification.AreaRatio] };
        var solution = RocketHost.Solve(fixture, mutated);
        Assert.Equal(CaseStatus.AreaRatioInvalid, solution.Status);
        Assert.Equal(CaseStatus.Ok, solution.StationStatus[0]);
        Assert.Equal(CaseStatus.Ok, solution.StationStatus[1]);
        Assert.Equal(CaseStatus.AreaRatioInvalid, solution.StationStatus[2]);
        Assert.Equal(CaseStatus.Ok, solution.StationStatus[3]);

        // The station after the failed one starts from the last converged station, so it reaches the same state to rounding level.
        var reference = RocketHost.Solve(fixture, inputs);
        Assert.Equal(reference.Stations[2].Temperature, solution.Stations[3].Temperature, reference.Stations[2].Temperature * 1e-9);
        Assert.Equal(reference.Figures[2].SpecificImpulse, solution.Figures[3].SpecificImpulse, reference.Figures[2].SpecificImpulse * 1e-9);
    }

    [Fact]
    public void A_pressure_ratio_not_above_one_fails_its_station_only()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var mutated = inputs with { ExitValues = [1.0, inputs.ExitValues[0]], ExitKinds = [ExitSpecification.PressureRatio, ExitSpecification.AreaRatio] };
        var solution = RocketHost.Solve(fixture, mutated);
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
        Assert.Equal(CaseStatus.InvalidInput, solution.StationStatus[2]);
        Assert.Equal(CaseStatus.Ok, solution.StationStatus[3]);
    }

    [Fact]
    public void A_case_without_exits_gives_the_chamber_and_the_throat()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var solution = RocketHost.Solve(fixture, inputs with { ExitValues = [], ExitKinds = [] });
        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.Equal(2, solution.StationCount);
        Assert.True(solution.Figures[1].CharacteristicVelocity > 0.0);
    }

    [Fact]
    public void A_non_positive_chamber_pressure_is_invalid_input()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var solution = RocketHost.Solve(fixture, inputs with { ChamberPressure = 0.0 });
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
        Assert.All(solution.StationStatus, s => Assert.Equal(CaseStatus.InvalidInput, s));
    }
}
