using AerospacePropellantThermodynamics.Execution;
using ProblemKind = AerospacePropellantThermodynamics.Equilibrium.ProblemKind;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// Reads the sweep part of a problem document (API.md, Input document) and finishes it: the engine's accelerator
/// choice and the assembly into <see cref="InputDocument"/>. <see cref="Complete"/> carries both because
/// <see cref="ProblemDocumentReader.Read"/> may not itself hold the sweep or the accelerator: either one, held there
/// alongside the problem-kind readers, pushed its efferent coupling past the root's limit (BOOT.md, Shape exceptions
/// of 2026-09-14 records the measurement).
/// </summary>
internal static class SweepDocumentReader
{
    public static InputDocument Complete(StrictObject root, PropellantDocument propellant, ProblemDocument problem)
    {
        var sweep = root.OptionalObject("sweep") is { } s ? Read(s, problem, propellant) : null;
        AcceleratorKind? accelerator = null;
        if (root.OptionalObject("engine") is { } engine)
        {
            accelerator = DocumentWords.ParseAccelerator(engine.String("accelerator"), engine.Path + ".accelerator");
            engine.Finish();
        }

        root.Finish();
        return new InputDocument(propellant, problem, sweep, accelerator);
    }

    private static SweepDocument Read(StrictObject sweep, ProblemDocument problem, PropellantDocument propellant)
    {
        IReadOnlyList<double>? Values(string name) => sweep.OptionalAny(name) is { } value ? SweepValues.Read(value, $"{sweep.Path}.{name}") : null;

        var ratios = Values("oxidizerToFuel");
        IReadOnlyList<double>? chamberPressures = null, pressures = null, temperatures = null;
        if (problem is RocketDocument)
        {
            chamberPressures = Values("chamberPressure");
        }
        else
        {
            pressures = Values("pressure");
            if (((EquilibriumDocument)problem).Kind == ProblemKind.AssignedTemperaturePressure)
            {
                temperatures = Values("temperature");
            }
        }

        sweep.Finish();
        if (ratios is not null && propellant is not ReactantPropellant { OxidizerToFuel: not null })
        {
            throw new InputException($"a sweep over oxidizerToFuel at {sweep.Path} needs a propellant given with mixture.oxidizerToFuel");
        }

        return new SweepDocument(ratios, chamberPressures, pressures, temperatures);
    }
}
