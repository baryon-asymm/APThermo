using AerospacePropellantThermodynamics.Performance;

namespace AerospacePropellantThermodynamics.Cli.Documents;

/// <summary>A rocket problem document: chamber pressure, flow model and exit stations.</summary>
internal sealed record RocketDocument(double ChamberPressure, FlowModel Flow, IReadOnlyList<double> AreaRatios, IReadOnlyList<double> PressureRatios,
                                      bool Transport, double TemperatureEstimate) : ProblemDocument;
