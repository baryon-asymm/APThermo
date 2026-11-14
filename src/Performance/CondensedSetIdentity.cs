using ILGPU;

namespace APThermo.Performance;

/// <summary>
/// Exact canonical identity of the at most twenty condensed scratch slots.
/// Four words hold five twelve-bit species keys each; zero denotes an empty slot.
/// Species indices are below Thermo's 2048-species limit, so index plus one fits.
/// </summary>
internal struct CondensedSetIdentity
{
    private ulong _first;
    private ulong _second;
    private ulong _third;
    private ulong _fourth;

    /// <summary>One species, useful also for an already-known boundary identity.</summary>
    internal static CondensedSetIdentity Single(int species)
    {
        var identity = default(CondensedSetIdentity);
        identity.Store(0, species);
        return identity;
    }

    /// <summary>Canonical ascending distinct keys; scratch order and vacant slots do not matter.</summary>
    internal static CondensedSetIdentity FromSlots(ArrayView<int> slots)
    {
        var identity = default(CondensedSetIdentity);
        var previous = -1;
        for (var position = 0; position < slots.Length; position++)
        {
            var next = int.MaxValue;
            for (var slot = 0; slot < slots.Length; slot++)
            {
                var species = slots[slot];
                if (species > previous && species < next)
                {
                    next = species;
                }
            }

            if (next == int.MaxValue)
            {
                break;
            }

            identity.Store(position, next);
            previous = next;
        }

        return identity;
    }

    /// <summary>Exact identity; no hash collision can certify equality.</summary>
    internal readonly bool SameAs(in CondensedSetIdentity other) =>
        _first == other._first && _second == other._second
        && _third == other._third && _fourth == other._fourth;

    private void Store(int position, int species)
    {
        var key = (ulong)(species + 1) << (12 * (position % 5));
        switch (position / 5)
        {
            case 0: _first |= key; break;
            case 1: _second |= key; break;
            case 2: _third |= key; break;
            case 3: _fourth |= key; break;
            default: break;
        }
    }
}
