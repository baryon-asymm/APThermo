namespace AerospacePropellantThermodynamics.Cli;

/// <summary>One named value of a station projection: exactly one of <see cref="Number"/> or <see cref="Integer"/> is set, matching the library field's own type.</summary>
internal readonly record struct Cell(string Name, double? Number, int? Integer)
{
    public static Cell Of(string name, double value) => new(name, value, null);

    public static Cell Of(string name, int value) => new(name, null, value);
}
