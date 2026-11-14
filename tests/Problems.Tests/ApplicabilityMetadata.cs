using System.Security.Cryptography;
using System.Text.Json.Nodes;
using APThermo.Data;
using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>Portable provenance and generated fit metadata of the separate A37 sidecar.</summary>
internal static class ApplicabilityMetadata
{
    internal static readonly string[] Names = ["graphite", "ice", "frozenAlumina"];

    internal static JsonObject Read() => JsonNode.Parse(File.ReadAllText(
        RepositoryPaths.Resolve("tests", "Fixtures", "AuditApplicabilityCases.json")))!.AsObject();

    internal static IReadOnlyList<string> Errors(JsonObject document, SpeciesDatabase database)
    {
        var errors = new List<string>();
        if (document["provenance"] is not JsonObject provenance)
        {
            return ["provenance is required"];
        }

        Check(provenance["thermoSha256"]?.GetValue<string>() == database.Provenance.ThermoSha256, "thermo hash", errors);
        Check(provenance["transSha256"]?.GetValue<string>() == database.Provenance.TransSha256, "transport hash", errors);
        Check(!string.IsNullOrWhiteSpace(provenance["method"]?.GetValue<string>()), "provenance method", errors);
        Check(!string.IsNullOrWhiteSpace(provenance["sourceArchive"]?.GetValue<string>()), "source archive", errors);
        CheckSources(provenance["auditSources"], errors);
        foreach (var name in Names)
        {
            if (document[name] is not JsonObject item) { errors.Add(name + " is required"); continue; }
            CheckFits(item, database, errors);
            CheckAdmissionSamples(item, errors);
        }

        if (document["frozenAlumina"] is JsonObject frozen) { CheckFixture(frozen, errors); }
        return errors;
    }

    private static void CheckSources(JsonNode? sources, List<string> errors)
    {
        if (sources is not JsonArray { Count: > 0 } list) { errors.Add("audit sources"); return; }
        foreach (var source in list)
        {
            var sha = source?["sha256"]?.GetValue<string>();
            Check(sha is { Length: 64 } && sha.All(char.IsAsciiHexDigit), "audit source hash", errors);
            Check(!string.IsNullOrWhiteSpace(source?["path"]?.GetValue<string>()), "audit source path", errors);
            Check(!string.IsNullOrWhiteSpace(source?["location"]?.GetValue<string>()), "audit source location", errors);
        }
    }

    private static void CheckFits(JsonObject item, SpeciesDatabase database, List<string> errors)
    {
        var name = item["species"]!.GetValue<string>();
        var raw = database.Records(name).SelectMany(record => record.Intervals).ToArray();
        Check(raw.Length > 0, name + " has no fits", errors);
        if (raw.Length == 0) { return; }
        var table = SpeciesTable.Build(database, Strings(item["elements"]!), [name]);
        var start = table.Arrays.IntervalStart[0];
        var count = table.Arrays.IntervalCount[0];
        var bounds = table.Arrays.IntervalBounds.Skip(2 * start).Take(2 * count).ToArray();
        CheckBound(item["rawFit"], "low", raw.Min(interval => interval.TLow), name + " raw lower", errors);
        CheckBound(item["rawFit"], "high", raw.Max(interval => interval.THigh), name + " raw upper", errors);
        CheckBound(item["tableFit"], "low", bounds.Where((_, index) => index % 2 == 0).Min(), name + " table lower", errors);
        CheckBound(item["tableFit"], "high", bounds.Where((_, index) => index % 2 == 1).Max(), name + " table upper", errors);
    }

    private static void CheckAdmissionSamples(JsonObject item, List<string> errors)
    {
        if (item["admissionLower"] is null) { return; }

        if (item["cases"] is not JsonArray { Count: > 0 } samples) { errors.Add("admission samples"); return; }
        var accepted = samples.Where(sample => sample!["condensatePresent"]!.GetValue<bool>()).ToArray();
        if (accepted.Length == 0) { errors.Add("accepted admission sample"); return; }
        var low = accepted.Min(sample => sample!["temperature"]!.GetValue<double>());
        Check(item["admissionLower"]!.GetValue<double>() == low, "admission lower witness", errors);
    }

    private static void CheckFixture(JsonObject item, List<string> errors)
    {
        var relative = item["inputFixture"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(relative)) { errors.Add("input fixture path"); return; }
        var path = RepositoryPaths.Resolve(relative);
        if (!File.Exists(path)) { errors.Add("input fixture missing"); return; }
        var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        Check(item["inputFixtureSha256"]?.GetValue<string>() == actual, "input fixture hash", errors);
    }

    private static void CheckBound(JsonNode? fit, string key, double actual, string message, List<string> errors) =>
        Check(fit is JsonObject range && range[key]?.GetValue<double>() == actual, message, errors);

    private static void Check(bool condition, string message, List<string> errors)
    {
        if (!condition) { errors.Add(message); }
    }

    internal static string[] Strings(JsonNode value) => [.. value.AsArray().Select(item => item!.GetValue<string>())];
}
