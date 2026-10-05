using System.Globalization;
using APThermo.Fixtures;

namespace APThermo.Thermo.Tests.Elementary;

/// <summary>
/// The oracle fixtures of <c>tests/Thermo.Tests/Elementary/fixtures</c> (<c>PROVENANCE.txt</c> there): one row per line,
/// the bit patterns of the inputs and of the exact result rounded to nearest even, or of the exact value as a
/// double-double for the margin files. The functions of the tree, called by name, so that one fact walks all three.
/// </summary>
internal static class ElementaryFixtures
{
    /// <summary>The functions the fixtures cover.</summary>
    public static readonly IReadOnlyList<string> Functions = ["exp", "log", "pow"];

    /// <summary>The directory of the fixture files.</summary>
    public static string Directory => RepositoryPaths.Resolve("tests", "Thermo.Tests", "Elementary", "fixtures");

    /// <summary>The number of inputs of a function: two for pow, one otherwise.</summary>
    public static int Arity(string function) => function == "pow" ? 2 : 1;

    /// <summary>The rows of a fixture file as doubles decoded from their bit patterns; comment lines are skipped.</summary>
    public static IReadOnlyList<double[]> Read(string file) =>
        [.. File.ReadLines(Path.Combine(Directory, file))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(' ').Select(Decode).ToArray())];

    /// <summary>The tree's function by name: the correctly rounded exp, log or pow of <c>KernelMath</c>.</summary>
    public static double Evaluate(string function, double[] row) =>
        function switch
        {
            "exp" => KernelMath.Exp(row[0]),
            "log" => KernelMath.Log(row[0]),
            _ => KernelMath.Pow(row[0], row[1]),
        };

    /// <summary>The 16 hexadecimal digits of a double's bit pattern.</summary>
    public static string Encode(double value) => BitConverter.DoubleToInt64Bits(value).ToString("X16", CultureInfo.InvariantCulture);

    private static double Decode(string token) =>
        BitConverter.Int64BitsToDouble(long.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}
