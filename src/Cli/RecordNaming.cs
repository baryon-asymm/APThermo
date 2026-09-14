using AerospacePropellantThermodynamics.Problems;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// A refusal of the front door (<see cref="StateRecordException"/>, <see cref="MixtureMassException"/>) renamed from
/// its index in the batch to the record's file and position, since the library names a record by its position in
/// the batch, not by the caller's own record.
/// </summary>
internal static class RecordNaming
{
    public static T Named<T>(IReadOnlyList<RecordSource> group, Func<T> solve)
    {
        try
        {
            return solve();
        }
        catch (StateRecordException e)
        {
            throw new InputException($"{group[e.Index].Label}: {e.Reason}");
        }
        catch (MixtureMassException e)
        {
            throw new InputException($"{group[e.Index].Label}: {e.Reason}");
        }
        catch (Exception e) when (e is ArgumentException or KeyNotFoundException)
        {
            // Every other library refusal at solve time (F-CL-13), translated where it is called.
            throw new InputException(e.Message);
        }
    }
}
