namespace APThermo.Thermo.Elementary;

/// <summary>The one rounding test of the three fast paths: a double-double rounds to one double whatever its error within the bound.</summary>
internal static class RoundingTest
{
    /// <summary>
    /// Whether <c>h + (l − ε·|h|)</c> and <c>h + (l + ε·|h|)</c> are the same double, ε = <paramref name="relativeError"/>:
    /// then every value within the bound of the double-double rounds to it, and <paramref name="rounded"/> is that double.
    /// </summary>
    /// <param name="f">The double-double returned by a fast path.</param>
    /// <param name="relativeError">The fast path's relative error bound.</param>
    /// <param name="rounded">The common double, meaningful when the test holds.</param>
    public static bool Certifies(Dd f, double relativeError, out double rounded)
    {
        var err = relativeError * Math.Abs(f.Hi);
        var left = f.Hi + (f.Lo - err);
        var right = f.Hi + (f.Lo + err);
        rounded = left;
        return left == right;
    }
}
