using System.Reflection;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Cli.Output;

/// <summary>
/// The one projection of a station into named, typed cells (the state, the performance figures with the two
/// conversions to seconds, the transport figures), from the library's structs by reflection (F-CL-06, F-CL-07).
/// Both <see cref="JsonOutput"/> and <see cref="CsvOutput"/> read a case through this one source, so a field added
/// to a library struct reaches both document forms without a second list. The public properties of the library's
/// result structs, in declaration order, with their document names.
/// </summary>
internal static class StationFields
{
    /// <summary>g0, m/s²: the one conversion of this node, to specific impulse in seconds.</summary>
    public const double StandardGravity = 9.80665;

    private static readonly IReadOnlyList<PropertyInfo> State_ = PropertiesOf<MixtureState>();
    private static readonly IReadOnlyList<PropertyInfo> Performance_ = PropertiesOf<PerformanceFigures>();
    private static readonly IReadOnlyList<PropertyInfo> Transport_ = PropertiesOf<TransportFigures>();

    public static IReadOnlyList<string> StateNames { get; } = [.. State_.Select(p => Names.Camel(p.Name))];

    public static IReadOnlyList<string> PerformanceNames { get; } =
        [.. Performance_.Select(p => Names.Camel(p.Name)), "specificImpulseSeconds", "vacuumSpecificImpulseSeconds"];

    public static IReadOnlyList<string> TransportNames { get; } = [.. Transport_.Select(p => Names.Camel(p.Name))];

    public static IReadOnlyList<Cell> State(MixtureState state) => [.. State_.Select(p => Cell.Of(Names.Camel(p.Name), (double)p.GetValue(state)!))];

    public static IReadOnlyList<Cell> Performance(PerformanceFigures figures)
    {
        var cells = Performance_.Select(p => Cell.Of(Names.Camel(p.Name), (double)p.GetValue(figures)!)).ToList();
        cells.Add(Cell.Of("specificImpulseSeconds", figures.SpecificImpulse / StandardGravity));
        cells.Add(Cell.Of("vacuumSpecificImpulseSeconds", figures.VacuumSpecificImpulse / StandardGravity));
        return cells;
    }

    public static IReadOnlyList<Cell> Transport(TransportFigures figures) => [.. Transport_.Select(p =>
    {
        var name = Names.Camel(p.Name);
        var value = p.GetValue(figures)!;
        return value is double d ? Cell.Of(name, d) : Cell.Of(name, (int)value);
    })];

    /// <summary>
    /// <see cref="Type.GetProperties()"/> does not document its order; declaration order is made explicit and
    /// guaranteed here, through the metadata token the compiler assigns in source order, so that the document's
    /// field and column order cannot depend on a runtime detail (the CSV column order and the JSON key order,
    /// API.md).
    /// </summary>
    private static List<PropertyInfo> PropertiesOf<T>() where T : struct =>
        [.. typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.MetadataToken)];
}
