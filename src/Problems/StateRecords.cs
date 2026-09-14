using AerospacePropellantThermodynamics.Equilibrium;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>Turns a state batch's records into the mixtures and equilibrium problems <see cref="Solver.SolveStates"/> runs.</summary>
internal static class StateRecords
{
    public static (List<ElementalMixture> Mixtures, List<EquilibriumProblem> Problems) ToProblems(IReadOnlyList<StateRecord> states, StateBatchOptions options)
    {
        var mixtures = new List<ElementalMixture>(states.Count);
        var problems = new List<EquilibriumProblem>(states.Count);
        for (var i = 0; i < states.Count; i++)
        {
            var record = states[i] ?? throw new ArgumentException($"state record {i} is null", nameof(states));
            var targets = (record.Enthalpy is not null ? 1 : 0) + (record.Temperature is not null ? 1 : 0) + (record.Entropy is not null ? 1 : 0);
            if (targets != 1)
            {
                throw new ArgumentException($"state record {i}: exactly one of enthalpy, temperature and entropy must be given, not {targets}", nameof(states));
            }

            try
            {
                mixtures.Add(ElementalMixture.Create(record.Composition, record.Enthalpy, options.Omit, options.Only, options.MassTolerance));
            }
            catch (ArgumentException inner)
            {
                throw new ArgumentException($"state record {i}: {inner.Message}", nameof(states), inner);
            }

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
}
