namespace AerospacePropellantThermodynamics.Transport;

/// <summary>The per-species runs collected by the build of a <see cref="TransportTable"/>; host side, written once.</summary>
internal sealed class SpeciesRuns
{
    /// <summary>Allocates the per-species arrays of a table of the given size.</summary>
    public SpeciesRuns(int speciesCount)
    {
        ViscosityStart = new int[speciesCount];
        ViscosityCount = new int[speciesCount];
        ConductivityStart = new int[speciesCount];
        ConductivityCount = new int[speciesCount];
    }

    /// <summary>[species] first fit of the viscosity run.</summary>
    public int[] ViscosityStart { get; }

    /// <summary>[species] viscosity fits; zero without data.</summary>
    public int[] ViscosityCount { get; }

    /// <summary>[species] first fit of the conductivity run.</summary>
    public int[] ConductivityStart { get; }

    /// <summary>[species] conductivity fits; zero without data.</summary>
    public int[] ConductivityCount { get; }

    /// <summary>Gaseous species with a viscosity fit, in table order.</summary>
    public List<string> WithData { get; } = [];

    /// <summary>Gaseous species without a transport entry, in table order.</summary>
    public List<string> WithoutData { get; } = [];
}

/// <summary>The pair runs collected by the build of a <see cref="TransportTable"/>; host side, written once.</summary>
internal sealed class PairRuns
{
    /// <summary>Allocates the dense pair index of a table of the given size, −1 everywhere.</summary>
    public PairRuns(int speciesCount)
    {
        Index = new int[speciesCount * speciesCount];
        Array.Fill(Index, -1);
    }

    /// <summary>[species * SpeciesCount + species] pair index, −1 without interaction data.</summary>
    public int[] Index { get; }

    /// <summary>[pair] first fit of the pair's run.</summary>
    public List<int> Start { get; } = [];

    /// <summary>[pair] fits of the pair.</summary>
    public List<int> Count { get; } = [];

    /// <summary>The pairs with data, in database order.</summary>
    public List<(string First, string Second)> Names { get; } = [];
}
