namespace AerospacePropellantThermodynamics.Data;

/// <summary>
/// The fixed-column layout of <c>thermo.inp</c> and <c>trans.inp</c> (BOOT.md, the format facts of both files), as
/// named constants, so the readers read against this table field by field instead of a bare number in place.
/// </summary>
internal static class RecordColumns
{
    // thermo.inp, record line 1: the identity (name, then a comment to the end of the line).
    public const int NameStart = 1;
    public const int NameLength = 18;

    // thermo.inp, record line 2: interval count, date code, up to five (symbol, count) formula pairs, phase, molar mass, formation enthalpy.
    public const int IntervalCountStart = 1;
    public const int IntervalCountLength = 2;
    public const int DateCodeStart = 4;
    public const int DateCodeLength = 6;
    public const int FormulaSymbolStart = 11;
    public const int FormulaSymbolLength = 2;
    public const int FormulaCountStart = 13;
    public const int FormulaCountLength = 6;
    public const int FormulaPairStride = 8;
    public const int FormulaPairs = 5;
    public const int PhaseStart = 51;
    public const int PhaseLength = 2;
    public const int MolarMassStart = 53;
    public const int MolarMassLength = 13;
    public const int FormationEnthalpyStart = 66;
    public const int FormationEnthalpyLength = 15;

    // thermo.inp, a record without intervals (N = 0): the assigned-enthalpy temperature (columns 1-11 by the
    // format facts) is read as the line's first whitespace token, so it has no entry here.

    // thermo.inp, an interval's header line: bounds, coefficient count, eight exponents, the enthalpy offset.
    public const int TLowStart = 1;
    public const int TLowLength = 11;
    public const int THighStart = 12;
    public const int THighLength = 11;
    public const int CoefficientCountStart = 23;
    public const int CoefficientCountLength = 1;
    public const int ExponentStart = 24;
    public const int ExponentLength = 5;
    public const int ExponentStride = 5;
    public const int EnthalpyOffsetStart = 66;
    public const int EnthalpyOffsetLength = 15;
    public const int CoefficientsPerInterval = 7;
    public const int ExponentsPerInterval = 8;

    // thermo.inp, an interval's two coefficient lines: a1 … a5 on the first; a6, a7 and, after a blank field, b1, b2 on the second.
    public const int CoefficientStart = 1;
    public const int CoefficientLength = 16;
    public const int CoefficientStride = 16;
    public const int B1Start = 49;
    public const int B1Length = 16;
    public const int B2Start = 65;
    public const int B2Length = 16;

    // trans.inp, a block header: one species name, or two for a binary interaction (the VnCm code and the reference follow, matched by TransportBlockReader's own pattern, not by column).
    public const int TransSpeciesStart = 1;
    public const int TransSpeciesLength = 16;
    public const int TransPartnerStart = 17;
    public const int TransPartnerLength = 16;

    // trans.inp, a fit line: the V/C kind letter, then TLow, THigh and four coefficients A, B, C, D.
    public const int FitKindStart = 2;
    public const int FitKindLength = 1;
    public const int FitTLowStart = 3;
    public const int FitTLowLength = 9;
    public const int FitTHighStart = 12;
    public const int FitTHighLength = 9;
    public const int FitAStart = 21;
    public const int FitALength = 15;
    public const int FitBStart = 36;
    public const int FitBLength = 15;
    public const int FitCStart = 51;
    public const int FitCLength = 15;
    public const int FitDStart = 66;
    public const int FitDLength = 15;
}
