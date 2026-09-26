namespace APThermo.Thermo;

/// <summary>The one physical constant of the tree.</summary>
internal static class PhysicalConstants
{
    /// <summary>Universal gas constant in J/(kmol·K), the value NASA CEA computes with.</summary>
    public const double R = 8314.51;
}

/// <summary>One station of one case, in SI units. Written by the numerical nodes, read by everyone.</summary>
public struct MixtureState : IEquatable<MixtureState>
{
    /// <summary>K.</summary>
    public double Temperature { get; set; }

    /// <summary>Pa.</summary>
    public double Pressure { get; set; }

    /// <summary>kg/m³.</summary>
    public double Density { get; set; }

    /// <summary>J/kg.</summary>
    public double Enthalpy { get; set; }

    /// <summary>J/kg, u = h − p/ρ.</summary>
    public double InternalEnergy { get; set; }

    /// <summary>J/(kg·K).</summary>
    public double Entropy { get; set; }

    /// <summary>J/kg.</summary>
    public double GibbsEnergy { get; set; }

    /// <summary>kg/kmol, CEA's M = 1/n: the whole mixture's mass per kilomole of gas.</summary>
    public double MolarMass { get; set; }

    /// <summary>kg/kmol, CEA's MW: one kilogram of mixture over the moles of all species, condensed included.</summary>
    public double MixtureMolarMass { get; set; }

    /// <summary>J/(kg·K), composition held fixed.</summary>
    public double CpFrozen { get; set; }

    /// <summary>J/(kg·K), composition shifting with temperature.</summary>
    public double CpEquilibrium { get; set; }

    /// <summary>J/(kg·K), composition held fixed.</summary>
    public double CvFrozen { get; set; }

    /// <summary>J/(kg·K), composition shifting.</summary>
    public double CvEquilibrium { get; set; }

    /// <summary>(∂ln V/∂ln T)_p, dimensionless; 1 for a frozen composition.</summary>
    public double DlnVdlnT { get; set; }

    /// <summary>(∂ln V/∂ln p)_T, dimensionless; −1 for a frozen composition.</summary>
    public double DlnVdlnP { get; set; }

    /// <summary>Isentropic exponent γ_s.</summary>
    public double GammaS { get; set; }

    /// <summary>m/s.</summary>
    public double SoundSpeed { get; set; }

    /// <summary>m/s; zero where not applicable.</summary>
    public double Velocity { get; set; }

    /// <summary>Dimensionless; zero where not applicable.</summary>
    public double Mach { get; set; }

    /// <summary>Every property equal to <paramref name="other"/>'s by <see cref="double.Equals(double)"/>, so NaN equals NaN.</summary>
    public readonly bool Equals(MixtureState other) =>
        Temperature.Equals(other.Temperature) &&
        Pressure.Equals(other.Pressure) &&
        Density.Equals(other.Density) &&
        Enthalpy.Equals(other.Enthalpy) &&
        InternalEnergy.Equals(other.InternalEnergy) &&
        Entropy.Equals(other.Entropy) &&
        GibbsEnergy.Equals(other.GibbsEnergy) &&
        MolarMass.Equals(other.MolarMass) &&
        MixtureMolarMass.Equals(other.MixtureMolarMass) &&
        CpFrozen.Equals(other.CpFrozen) &&
        CpEquilibrium.Equals(other.CpEquilibrium) &&
        CvFrozen.Equals(other.CvFrozen) &&
        CvEquilibrium.Equals(other.CvEquilibrium) &&
        DlnVdlnT.Equals(other.DlnVdlnT) &&
        DlnVdlnP.Equals(other.DlnVdlnP) &&
        GammaS.Equals(other.GammaS) &&
        SoundSpeed.Equals(other.SoundSpeed) &&
        Velocity.Equals(other.Velocity) &&
        Mach.Equals(other.Mach);

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is MixtureState other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Temperature);
        hash.Add(Pressure);
        hash.Add(Density);
        hash.Add(Enthalpy);
        hash.Add(InternalEnergy);
        hash.Add(Entropy);
        hash.Add(GibbsEnergy);
        hash.Add(MolarMass);
        hash.Add(MixtureMolarMass);
        hash.Add(CpFrozen);
        hash.Add(CpEquilibrium);
        hash.Add(CvFrozen);
        hash.Add(CvEquilibrium);
        hash.Add(DlnVdlnT);
        hash.Add(DlnVdlnP);
        hash.Add(GammaS);
        hash.Add(SoundSpeed);
        hash.Add(Velocity);
        hash.Add(Mach);
        return hash.ToHashCode();
    }

    /// <summary>Value equality, field by field.</summary>
    public static bool operator ==(MixtureState left, MixtureState right) => left.Equals(right);

    /// <summary>Value inequality, field by field.</summary>
    public static bool operator !=(MixtureState left, MixtureState right) => !left.Equals(right);
}

/// <summary>Per-case outcome of a numerical routine. Numerical code never throws; it reports one of these.</summary>
public enum CaseStatus
{
    /// <summary>The case solved successfully.</summary>
    Ok = 0,

    /// <summary>The case's input was invalid (for example, out-of-range or inconsistent parameters).</summary>
    InvalidInput,

    /// <summary>The iterative solver did not converge within its step budget.</summary>
    NotConverged,

    /// <summary>The solver's derivative matrix was singular and could not be inverted.</summary>
    SingularMatrix,

    /// <summary>The temperature fell outside the range the species table covers.</summary>
    TemperatureOutOfRange,

    /// <summary>The throat search did not find a throat.</summary>
    ThroatNotFound,

    /// <summary>The requested area ratio is not attainable for the case.</summary>
    AreaRatioInvalid,

    /// <summary>No transport data is available for the case's species.</summary>
    NoTransportData,
}

/// <summary>Size limits of a species table; they size the scratch of every consumer.</summary>
internal static class TableLimits
{
    public const int MaxElements = 20;

    public const int MaxSpecies = 2048;

    public const int MaxIntervalsPerSpecies = 5;
}
