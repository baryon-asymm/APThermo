namespace APThermo.Thermo;

/// <summary>The one physical constant of the tree.</summary>
internal static class PhysicalConstants
{
    /// <summary>Universal gas constant in J/(kmol·K), the value NASA CEA computes with.</summary>
    public const double R = 8314.51;
}

/// <summary>One station of one case, in SI units. Written by the numerical nodes, read by everyone.</summary>
public struct MixtureState
{
    /// <summary>K.</summary>
    public double Temperature;

    /// <summary>Pa.</summary>
    public double Pressure;

    /// <summary>kg/m³.</summary>
    public double Density;

    /// <summary>J/kg.</summary>
    public double Enthalpy;

    /// <summary>J/kg, u = h − p/ρ.</summary>
    public double InternalEnergy;

    /// <summary>J/(kg·K).</summary>
    public double Entropy;

    /// <summary>J/kg.</summary>
    public double GibbsEnergy;

    /// <summary>kg/kmol, CEA's M = 1/n: the whole mixture's mass per kilomole of gas.</summary>
    public double MolarMass;

    /// <summary>kg/kmol, CEA's MW: one kilogram of mixture over the moles of all species, condensed included.</summary>
    public double MixtureMolarMass;

    /// <summary>J/(kg·K), composition held fixed.</summary>
    public double CpFrozen;

    /// <summary>J/(kg·K), composition shifting with temperature.</summary>
    public double CpEquilibrium;

    /// <summary>J/(kg·K), composition held fixed.</summary>
    public double CvFrozen;

    /// <summary>J/(kg·K), composition shifting.</summary>
    public double CvEquilibrium;

    /// <summary>(∂ln V/∂ln T)_p, dimensionless; 1 for a frozen composition.</summary>
    public double DlnVdlnT;

    /// <summary>(∂ln V/∂ln p)_T, dimensionless; −1 for a frozen composition.</summary>
    public double DlnVdlnP;

    /// <summary>Isentropic exponent γ_s.</summary>
    public double GammaS;

    /// <summary>m/s.</summary>
    public double SoundSpeed;

    /// <summary>m/s; zero where not applicable.</summary>
    public double Velocity;

    /// <summary>Dimensionless; zero where not applicable.</summary>
    public double Mach;
}

/// <summary>Per-case outcome of a numerical routine. Numerical code never throws; it reports one of these.</summary>
public enum CaseStatus
{
    Ok = 0,
    InvalidInput,
    NotConverged,
    SingularMatrix,
    TemperatureOutOfRange,
    ThroatNotFound,
    AreaRatioInvalid,
    NoTransportData,
}

/// <summary>Size limits of a species table; they size the scratch of every consumer.</summary>
internal static class TableLimits
{
    public const int MaxElements = 20;

    public const int MaxSpecies = 2048;

    public const int MaxIntervalsPerSpecies = 5;
}
