using System.Text.Json.Nodes;

namespace APThermo.Problems.Tests;

/// <summary>Metadata guards are separate from the public-policy witnesses and from scientific reference tests.</summary>
public sealed class ApplicabilityMetadataTests
{
    /// <summary>Data and exact source input bytes match, and raw/table fit bounds are generated from loaded records.</summary>
    [Fact]
    public void TheSidecarMatchesItsDataProvenanceAndGeneratedFits() =>
        Assert.Empty(ApplicabilityMetadata.Errors(ApplicabilityMetadata.Read(), SolverFixture.SharedDatabase));

    /// <summary>Missing provenance, mismatched data/input hashes and corrupt fit bounds cannot pass as applicability evidence.</summary>
    [Theory]
    [InlineData("provenance")]
    [InlineData("thermoSha256")]
    [InlineData("transSha256")]
    [InlineData("sourceArchive")]
    [InlineData("auditSources")]
    [InlineData("sourceSha256")]
    [InlineData("rawFit")]
    [InlineData("tableFit")]
    [InlineData("admissionLower")]
    [InlineData("cases")]
    [InlineData("inputFixture")]
    [InlineData("inputFixtureSha256")]
    [InlineData("graphite")]
    public void DamagedMetadataIsRejected(string fault)
    {
        var document = ApplicabilityMetadata.Read();
        Damage(document, fault);
        Assert.NotEmpty(ApplicabilityMetadata.Errors(document, SolverFixture.SharedDatabase));
    }

    private static void Damage(JsonObject document, string fault)
    {
        switch (fault)
        {
            case "provenance":
            case "graphite": _ = document.Remove(fault); break;
            case "thermoSha256":
            case "transSha256":
            case "sourceArchive": document["provenance"]![fault] = ""; break;
            case "auditSources": document["provenance"]![fault] = new JsonArray(); break;
            case "sourceSha256": document["provenance"]!["auditSources"]![0]!["sha256"] = ""; break;
            case "rawFit":
            case "tableFit": document["graphite"]![fault]!["low"] = -1.0; break;
            case "admissionLower": document["graphite"]![fault] = -1.0; break;
            case "cases": document["graphite"]![fault] = new JsonArray(); break;
            case "inputFixture":
            case "inputFixtureSha256": document["frozenAlumina"]![fault] = "missing"; break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
    }
}
