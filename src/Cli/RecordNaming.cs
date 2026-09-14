using AerospacePropellantThermodynamics.Problems;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// A refusal of the front door (<see cref="MixtureMassException"/>) renamed from its index in the batch to the
/// record's file and position, since the library names a mixture by its position, not by the caller's record.
/// </summary>
internal static class RecordNaming
{
    public static T Named<T>(IReadOnlyList<StateDocument> group, Func<T> solve)
    {
        try
        {
            return solve();
        }
        catch (MixtureMassException e)
        {
            throw new InputException($"{group[e.Index].Source}: {e.Reason}");
        }
        catch (Exception e) when (e is ArgumentException or KeyNotFoundException)
        {
            // Every other library refusal at solve time (F-CL-13), translated where it is called.
            throw new InputException(e.Message);
        }
    }

    public static ElementalMixture MixtureOf(StateDocument record, double massTolerance)
    {
        try
        {
            return ElementalMixture.Create(record.Composition, record.Enthalpy, massTolerance: massTolerance);
        }
        catch (ArgumentException e)
        {
            throw new InputException($"{record.Source}: {e.Message}");
        }
    }
}
