using APThermo.Equilibrium;
using APThermo.Performance;

namespace APThermo.Problems;

/// <summary>
/// Turns a state batch's records into the mixtures and problems <see cref="Solver.SolveStates"/> and
/// <see cref="Solver.SolveRocketStates"/> run, and owns every rule of a record's shape (BOOT.md): exactly one target;
/// exits need an enthalpy; a flow only with exits; a record with exits belongs to <see cref="Solver.SolveRocketStates"/>
/// and one without to <see cref="Solver.SolveStates"/>. Every refusal of a record is a <see cref="StateRecordException"/>
/// with a subject-free reason; the mass check keeps <see cref="MixtureMassException"/>, which carries the index already.
/// </summary>
internal static class StateRecords
{
    public static (List<ElementalMixture> Mixtures, List<EquilibriumProblem> Problems) ToEquilibriumProblems(IReadOnlyList<StateRecord> states, StateBatchOptions options)
    {
        var mixtures = new List<ElementalMixture>(states.Count);
        var problems = new List<EquilibriumProblem>(states.Count);
        for (var i = 0; i < states.Count; i++)
        {
            var record = Validate(states, i, expectsExits: false);
            mixtures.Add(MixtureOf(record, options, i));
            problems.Add(new EquilibriumProblem
            {
                Kind = record.Temperature is not null ? ProblemKind.AssignedTemperaturePressure
                     : record.Entropy is not null ? ProblemKind.AssignedEntropyPressure
                     : ProblemKind.AssignedEnthalpyPressure,
                Pressure = record.Pressure,
                Temperature = record.Temperature ?? 0.0,
                Enthalpy = record.Enthalpy,
                Entropy = record.Entropy ?? 0.0,
                Transport = options.Transport,
            });
        }

        return (mixtures, problems);
    }

    public static (List<ElementalMixture> Mixtures, List<RocketProblem> Problems) ToRocketProblems(IReadOnlyList<StateRecord> states, StateBatchOptions options)
    {
        var mixtures = new List<ElementalMixture>(states.Count);
        var problems = new List<RocketProblem>(states.Count);
        for (var i = 0; i < states.Count; i++)
        {
            var record = Validate(states, i, expectsExits: true);
            mixtures.Add(MixtureOf(record, options, i));
            problems.Add(new RocketProblem
            {
                ChamberPressure = record.Pressure,
                Flow = record.Flow ?? FlowModel.ShiftingEquilibrium,
                PressureRatios = record.PressureRatios,
                AreaRatios = record.AreaRatios,
                Transport = options.Transport,
            });
        }

        return (mixtures, problems);
    }

    /// <summary>Every rule of a record's shape but the mass; <paramref name="expectsExits"/> is the caller's own kind (<see langword="true"/> for <see cref="Solver.SolveRocketStates"/>).</summary>
    private static StateRecord Validate(IReadOnlyList<StateRecord> states, int index, bool expectsExits)
    {
        var record = states[index] ?? throw new StateRecordException(index, "the record is null");
        var targets = (record.Enthalpy is not null ? 1 : 0) + (record.Temperature is not null ? 1 : 0) + (record.Entropy is not null ? 1 : 0);
        if (targets != 1)
        {
            throw new StateRecordException(index, $"exactly one of enthalpy, temperature and entropy must be given, not {targets}");
        }

        if (record.HasExits && record.Enthalpy is null)
        {
            throw new StateRecordException(index, "a record with exits needs an enthalpy, not a temperature or an entropy");
        }

        if (record.Flow is not null && !record.HasExits)
        {
            throw new StateRecordException(index, "a flow model needs exits; it has no meaning on a record without them");
        }

        if (record.HasExits != expectsExits)
        {
            throw new StateRecordException(index, expectsExits
                ? "the record has no exits; call SolveStates instead"
                : "the record has exits; call SolveRocketStates instead");
        }

        return record;
    }

    private static ElementalMixture MixtureOf(StateRecord record, StateBatchOptions options, int index)
    {
        try
        {
            return ElementalMixture.Create(record.Composition, record.Enthalpy, options.Omit, options.Only, options.MassTolerance);
        }
        catch (ArgumentException inner)
        {
            throw new StateRecordException(index, inner.Message);
        }
    }
}
