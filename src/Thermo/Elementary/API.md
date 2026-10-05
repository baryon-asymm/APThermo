# API.md — Thermo.Elementary

Namespace `APThermo.Thermo.Elementary`. Every type is `internal` to the thermo assembly: the numerical nodes reach
the functions only through `KernelMath` ([Thermo](../API.md), "Kernel minimum and maximum"), and the tests node reaches
the rest (the fast paths, the accurate paths, the tables) because the facts of
[the node's BOOT.md](BOOT.md) are about them. Everything not listed here is private to this node and may change
without notice.

## Elementary functions (tree contract) ✅

```csharp
namespace APThermo.Thermo.Elementary;

internal static class ExpFunction
{
    public const double XMax = 709.782712893384;            // above this exp is +infinity
    public const double XZero = -746.0;                      // below this exp is +0
    public const double XSubnormal = -708.3964185322641;     // below this the result is subnormal: accurate path only
    public const double FastError = 2.117582368135751e-22;   // 2^-72, the fast path's relative error bound

    public static double Exp(double x);                      // correctly rounded; NoInlining
    public static Dd Fast(double x, double xl, out int k);   // exp(x + xl) / 2^(k >> 6) as a double-double
    public static double Accurate(double x, double xl);      // the triple-double path, rounded; NoInlining
    public static Td AccurateTd(Td x, out int e);            // exp(x) / 2^e as a triple-double
}

internal static class LogFunction
{
    public const double FastError = 5.293955920339377e-23;   // 2^-74

    public static double Log(double x);                      // correctly rounded; NoInlining
    public static Dd Fast(double x);                         // log(x) as a double-double, x positive and finite
    public static Td AccurateTd(double x);                   // log(x) as a triple-double; NoInlining
}

internal static class PowFunction
{
    public static double Pow(double x, double y);            // correctly rounded, C99 special values; NoInlining
    public static Dd LogProduct(double x, double y);         // y * log(x) as a double-double, x positive and finite
    public static double FastErrorBound(double z);           // ExpFunction.FastError + |z| LogFunction.FastError
    public static double Accurate(double x, double y);       // exact midpoint, else triple-double; NoInlining
}

internal static class ExactPower
{
    public static bool TryRoundMidpoint(double x, double y, out double result);   // x^y exactly a midpoint of two doubles: the tie-to-even result
}

internal static class RoundingTest
{
    public static bool Certifies(Dd f, double relativeError, out double rounded);   // h + (l - eps|h|) == h + (l + eps|h|)
}
```

`Exp`, `Log` and `Pow` are the contract of `KernelMath.Exp`, `Log` and `Pow`: the exact value rounded to nearest, ties to even,
on every double input, the special values of IEEE 754 and C99 Annex F, on both accelerators and every platform. A NaN
operand gives the first NaN operand; a NaN made of non-NaN operands is `double.NaN`. The other members expose the two phases
for the facts: a fast path returns a double-double of relative error below the bound named next to it, the rounding test
accepts it only when every value within the bound rounds alike, and the accurate path (error below 2^-125) rounds the rest.

## Arithmetic and tables (tree contract) ✅

```csharp
namespace APThermo.Thermo.Elementary;

internal readonly struct Dd
{
    public Dd(double hi, double lo);
    public double Hi { get; }
    public double Lo { get; }
}

internal readonly struct Td
{
    public Td(double h, double m, double l);
    public double H { get; }
    public double M { get; }
    public double L { get; }
}

internal readonly struct LogTableEntry
{
    public LogTableEntry(double c, double hi, double lo);
    public double C { get; }                                 // RN(1/(1 + i/32))
    public double Hi { get; }                                // -log C as a double-double
    public double Lo { get; }
}

internal static class ErrorFree
{
    public static double Fma(double a, double b, double c);  // Math.FusedMultiplyAdd: the one fused operation of the tree
    public static Dd TwoSum(double a, double b);
    public static Dd FastTwoSum(double a, double b);         // |a| >= |b| or a == 0
    public static Dd TwoProd(double a, double b);
    public static double Pow2(int e);                        // 2^e, e in -1022 .. 1023
    public static double Scale(double v, int e);             // v * 2^e in two exact factors
}

internal static class TripleDouble
{
    public static Td Of(double a);
    public static Td Renorm(double x0, double x1, double x2, double x3, double x4);
    public static Td Add(Td a, Td b);
    public static Td Mul(Td a, Td b);
    public static Td MulD(Td a, double b);
    public static Td DivD(Td a, double n);
    public static Td Div(Td a, Td b);
    public static double Round(Td v);                        // nearest even, normal range
    public static double RoundScaled(Td v, int e);           // v * 2^e rounded, subnormals and overflow included
}

internal static class ExpTable
{
    public static Dd At(int j);                              // 2^(j/64), j in 0 .. 63; generated
}

internal static class LogTable
{
    public static LogTableEntry At(int i);                   // i in 0 .. 32; generated
}

internal static class ElementaryConstants
{
    public const double ExpInverseStep = 92.33248261689366;      // 64/ln 2
    public const double ExpStepHigh = 0.010830424696249145;      // RN(ln 2/64)
    public const double ExpStepLow = 3.623510646634843e-19;
    public const double Ln2High = 0.6931471805599453;
    public const double Ln2Low = 2.3190468138462996e-17;
    public const double Ln2T0 = 0.6931471805599453;
    public const double Ln2T1 = 2.3190468138462996e-17;
    public const double Ln2T2 = 5.707708438416212e-34;
    public const double InverseLn2 = 1.4426950408889634;
    public const double ThirdHigh = 0.3333333333333333;
    public const double ThirdLow = 1.850371707708594e-17;
}
```

`ExpTable`, `LogTable` and `ElementaryConstants` are written by
`tests/Thermo.Tests/Elementary/generate_tables.py` and carry its header; the values above are the generator's.

## Errors

None: no member throws. An argument outside a function's domain returns what IEEE 754 returns (NaN, ±∞, ±0).

## Side effects

None.

## Out of scope

- The decision which function a node calls and the minimum and maximum: `KernelMath` of [Thermo](../API.md).
- Hyperbolic and trigonometric functions, `Log10`, `Log2`, `Exp2`: no numerical node needs them (owner decision O3, 2026-10-05).
