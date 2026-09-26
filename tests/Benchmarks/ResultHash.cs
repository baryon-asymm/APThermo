using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Benchmarks;

/// Adds the bits of a solved state to a `BitHash` (BOOT.md, Invariants: every
/// benchmark that solves records, once per configuration, the statuses of its cases
/// and a hash of its results — the bits of the result structs and moles).
internal static class ResultHash
{
    public static BitHash AddState(this BitHash hash, MixtureState state) => hash
        .Add(state.Temperature).Add(state.Pressure).Add(state.Density)
        .Add(state.Enthalpy).Add(state.InternalEnergy).Add(state.Entropy).Add(state.GibbsEnergy)
        .Add(state.MolarMass).Add(state.MixtureMolarMass)
        .Add(state.CpFrozen).Add(state.CpEquilibrium).Add(state.CvFrozen).Add(state.CvEquilibrium)
        .Add(state.DlnVdlnT).Add(state.DlnVdlnP).Add(state.GammaS).Add(state.SoundSpeed)
        .Add(state.Velocity).Add(state.Mach);

    public static BitHash AddMoles(this BitHash hash, IReadOnlyDictionary<string, double> moleFractions)
    {
        foreach (var species in moleFractions.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            _ = hash.Add(moleFractions[species]);
        }
        return hash;
    }

    public static BitHash AddStatus(this BitHash hash, CaseStatus status) => hash.Add((int)status);
}
