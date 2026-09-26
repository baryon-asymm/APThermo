namespace APThermo.Transport.Tests;

/// <summary>
/// The committed list of trace-eliminated stations (<c>Transport.Tests/BOOT.md</c>, the ⚠ of 2026-09-26, guards audit F1): the
/// stations where the reference's reacting conductivity carries the documented defect (<c>src/Transport/BOOT.md</c>'s ⚠ of
/// 2026-09-12) and the tree's own solver reports a trace elimination (<see cref="TransportFigures.TraceEliminations"/> &gt; 0).
/// The list is pinned so that an unlisted elimination, or a listed station that has stopped showing the defect, fails a fact
/// that runs in the hosted matrix, not only in the bit snapshot (guards audit F1).
/// </summary>
internal static class TraceEliminatedStations
{
    /// <summary>
    /// Generated 2026-09-27 by evaluating every station of every rocket fixture run with transport
    /// (<see cref="TransportHost.RocketCaseNamesWithTransport"/> and <see cref="TransportHost.EvaluateStations(CpuFixture, APThermo.Fixtures.CeaCase)"/>)
    /// on the CPU accelerator, and keeping every station whose <see cref="TransportFigures.TraceEliminations"/> is positive,
    /// tree at <c>89bb619</c>. Nine stations: the LOX/LH2 O/F 4 exits at three chamber pressures, and the O/F 5 exit2 at the
    /// same three pressures (the hidden-defect audit's finding of 2026-09-12, restated by the guards audit of 2026-09-26 as
    /// F1).
    /// </summary>
    public static readonly IReadOnlyList<(string FixturePath, string Station)> Keys =
    [
        ("tests/Fixtures/cases/rocket/lox-lh2_of4_pc10MPa_shiftingEquilibrium.json", "exit1"),
        ("tests/Fixtures/cases/rocket/lox-lh2_of4_pc10MPa_shiftingEquilibrium.json", "exit2"),
        ("tests/Fixtures/cases/rocket/lox-lh2_of4_pc5MPa_shiftingEquilibrium.json", "exit1"),
        ("tests/Fixtures/cases/rocket/lox-lh2_of4_pc5MPa_shiftingEquilibrium.json", "exit2"),
        ("tests/Fixtures/cases/rocket/lox-lh2_of4_pc7MPa_shiftingEquilibrium.json", "exit1"),
        ("tests/Fixtures/cases/rocket/lox-lh2_of4_pc7MPa_shiftingEquilibrium.json", "exit2"),
        ("tests/Fixtures/cases/rocket/lox-lh2_of5_pc10MPa_shiftingEquilibrium.json", "exit2"),
        ("tests/Fixtures/cases/rocket/lox-lh2_of5_pc5MPa_shiftingEquilibrium.json", "exit2"),
        ("tests/Fixtures/cases/rocket/lox-lh2_of5_pc7MPa_shiftingEquilibrium.json", "exit2"),
    ];
}
