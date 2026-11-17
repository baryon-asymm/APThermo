using System.Text.Json;
using System.Xml.Linq;
using APThermo.Execution.Tests;
using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace APThermo.GpuEvidence;

internal static class Program
{
    private static readonly string FirstSweep = typeof(CudaTests).FullName + "." + nameof(CudaTests.TheSweepOf100000CasesOnCudaEqualsTheCpuAcceleratorBitForBitAndIsDeterministic);
    private static readonly string SecondSweep = typeof(CudaTests).FullName + "." + nameof(CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio);
    private const string Marker = "CUDA_EVIDENCE:";

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "discover")
            {
                Discover(args[1]);
            }
            else if (args.Length == 4 && args[0] == "verify")
            {
                Verify(args[1], args[2], args[3]);
            }
            else
            {
                throw new ArgumentException("Expected discover <inventory> or verify <inventory> <remainder-trx-name> <sweep-trx-name>.");
            }

            return 0;
        }
        catch (InvalidOperationException error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Discover(string output)
    {
        var sdk = Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "sdk"));
        var sdkVersion = Environment.GetEnvironmentVariable("GPU_EVIDENCE_SDK") ?? throw new InvalidOperationException("GPU_EVIDENCE_SDK must name the selected SDK directory.");
        var console = Path.Combine(sdk, sdkVersion, "vstest.console.dll");
        var projects = File.ReadAllLines("APThermo.sln")
            .SelectMany(line => line.Split('"'))
            .Where(value => value.StartsWith("tests", StringComparison.Ordinal) && value.EndsWith(".Tests.csproj", StringComparison.Ordinal))
            .Select(value => value.Replace('\\', Path.DirectorySeparatorChar)).ToArray();
        if (projects.Length == 0 || !File.Exists(console))
        {
            throw new InvalidOperationException("SDK console or solution test projects missing.");
        }

        var all = new List<Case>();
        foreach (var project in projects)
        {
            var assembly = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, "bin", "Release", "net10.0", Path.GetFileNameWithoutExtension(project) + ".dll"));
            var adapter = Path.Combine(Path.GetDirectoryName(assembly)!, "xunit.runner.visualstudio.testadapter.dll");
            if (!File.Exists(assembly) || !File.Exists(adapter))
            {
                throw new InvalidOperationException("Built test assembly or adapter missing: " + project);
            }

            var capture = new Capture();
            var wrapper = new VsTestConsoleWrapper(console);
            try
            {
                wrapper.StartSession();
                wrapper.InitializeExtensions([adapter]);
                wrapper.DiscoverTests([assembly], null!, new TestPlatformOptions(), capture);
                capture.EnsureComplete(project);

                all.AddRange(capture.Cases.Where(item => item.Categories.Contains("Cuda", StringComparer.OrdinalIgnoreCase) || item.Categories.Contains("BitSnapshot", StringComparer.OrdinalIgnoreCase)));
            }
            finally
            {
                wrapper.EndSession();
            }
        }

        _ = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(all));
        Console.WriteLine($"Discovered {all.Count} selected cases across {projects.Length} projects.");
    }

    private static void Verify(string inventory, string remainderName, string sweepName)
    {
        var expected = JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(inventory)) ?? throw new InvalidOperationException("Missing inventory.");
        var results = new List<(string Id, string Name, string Outcome, string Output, bool Sweep)>();
        foreach (var (filename, sweep) in new[] { (remainderName, false), (sweepName, true) })
        {
            var reports = Directory.GetFiles("tests", filename, SearchOption.AllDirectories).Where(path => Path.GetFileName(Path.GetDirectoryName(path)) == "TestResults").ToArray();
            if (reports.Length == 0)
            {
                throw new InvalidOperationException("Missing report: " + filename);
            }

            foreach (var report in reports)
            {
                var document = XDocument.Load(report);
                XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
                var summary = document.Descendants(ns + "ResultSummary").Single();
                var counters = summary.Elements(ns + "Counters").Single();
                var items = document.Descendants(ns + "UnitTestResult").ToArray();
                var executionIds = items.Select(item => (string?)item.Attribute("executionId")).ToArray();
                if ((string?)summary.Attribute("outcome") != "Completed" ||
                    (int?)counters.Attribute("total") != items.Length ||
                    (int?)counters.Attribute("executed") != items.Length ||
                    (int?)counters.Attribute("passed") != items.Length ||
                    (int?)counters.Attribute("failed") != 0 ||
                    counters.Attributes().Where(attribute => attribute.Name.LocalName is not ("total" or "executed" or "passed"))
                        .Any(attribute => !int.TryParse(attribute.Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) || value != 0) ||
                    executionIds.Any(string.IsNullOrEmpty) || executionIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
                {
                    throw new InvalidOperationException("Incomplete or inconsistent report summary: " + report);
                }

                results.AddRange(items.Select(item => ((string?)item.Attribute("testId") ?? "", (string?)item.Attribute("testName") ?? "", (string?)item.Attribute("outcome") ?? "", item.Element(ns + "Output")?.Value ?? "", sweep)));
            }
        }

        if (expected.Count == 0 || !results.Any(item => !item.Sweep) || results.Count(item => item.Sweep) != 2)
        {
            throw new InvalidOperationException("Missing selected cases, remainder or sweep pair.");
        }

        foreach (var item in expected)
        {
            var matches = results.Where(result => string.Equals(result.Id, item.Id, StringComparison.OrdinalIgnoreCase) && result.Name == item.Name).ToArray();
            if (matches.Length != 1 || matches[0].Outcome != "Passed" || matches[0].Sweep != (item.Fqn == FirstSweep || item.Fqn == SecondSweep))
            {
                throw new InvalidOperationException("Missing, duplicated, failed or misplaced case: " + item.Name);
            }

            if (item.Categories.Contains("Cuda", StringComparer.OrdinalIgnoreCase))
            {
                CheckMarker(item, matches[0].Output);
            }
        }

        if (expected.Count != results.Count || expected.Select(item => (item.Id, item.Name)).Distinct().Count() != expected.Count || expected.Count(item => item.Fqn == FirstSweep) != 1 || expected.Count(item => item.Fqn == SecondSweep) != 1)
        {
            throw new InvalidOperationException("Unexpected or duplicated cases, or missing sweep facts.");
        }

        Console.WriteLine($"Verified {results.Count} exact identities and CUDA markers.");
    }

    private static void CheckMarker(Case item, string output)
    {
        var markers = output.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith(Marker, StringComparison.Ordinal)).ToArray();
        if (markers.Length != 1)
        {
            throw new InvalidOperationException("Expected exactly one CUDA marker: " + item.Name);
        }

        using var document = JsonDocument.Parse(markers[0][Marker.Length..]);
        var root = document.RootElement;
        var properties = root.EnumerateObject().Select(property => property.Name).ToArray();
        if (properties.Distinct(StringComparer.Ordinal).Count() != properties.Length)
        {
            throw new InvalidOperationException("Duplicate CUDA marker field: " + item.Name);
        }

        var scope = root.GetProperty("scope").GetString();
        var actual = root.GetProperty("actualCuda").GetBoolean();
        var refusal = root.GetProperty("refusalVerified").GetBoolean();
        var scenario = root.GetProperty("scenarioExecuted");
        var device = root.GetProperty("device");
        if ((scenario.ValueKind != JsonValueKind.Null && scenario.ValueKind != JsonValueKind.True && scenario.ValueKind != JsonValueKind.False) ||
            (device.ValueKind != JsonValueKind.Null && device.ValueKind != JsonValueKind.String) ||
            scope != item.Scope || refusal ||
            (scope == "hardware" && (!actual || scenario.ValueKind != JsonValueKind.True || string.IsNullOrWhiteSpace(device.GetString()))) ||
            (scope == "driver-only" && (actual || scenario.ValueKind != JsonValueKind.True)) ||
            (scope != "hardware" && scope != "driver-only"))
        {
            throw new InvalidOperationException("CUDA marker does not prove its declared scope: " + item.Name);
        }
    }
}

internal sealed record Case(string Id, string Name, string Fqn, string[] Categories, string? Scope);

internal sealed class Capture : ITestDiscoveryEventsHandler2
{
    public List<Case> Cases { get; } = [];
    public long Count { get; private set; }
    public bool Aborted { get; private set; }
    public bool HasError { get; private set; }
    public int CompletionCount { get; private set; }

    public void EnsureComplete(string project)
    {
        if (HasError || Aborted || CompletionCount != 1 || Count != Cases.Count)
        {
            throw new InvalidOperationException("Incomplete discovery: " + project);
        }
    }

    public void HandleDiscoveredTests(IEnumerable<TestCase>? discoveredTestCases) => Add(discoveredTestCases);
    public void HandleDiscoveryComplete(DiscoveryCompleteEventArgs arguments, IEnumerable<TestCase>? lastChunk)
    {
        CompletionCount++;
        Count = arguments.TotalCount;
        Aborted = arguments.IsAborted;
        Add(lastChunk);
    }

    public void HandleLogMessage(TestMessageLevel level, string? message)
    {
        HasError |= level == TestMessageLevel.Error;
        Console.Error.WriteLine($"{level}: {message}");
    }
    public void HandleRawMessage(string rawMessage) { }

    private void Add(IEnumerable<TestCase>? cases)
    {
        if (cases is null)
        {
            return;
        }
        foreach (var item in cases)
        {
            var property = item.Properties.SingleOrDefault(value => value.Id == "TestObject.Traits");
            var traits = property is null ? [] : (KeyValuePair<string, string>[]?)item.GetPropertyValue(property) ?? [];
            var categories = traits.Where(value => value.Key == "Category").Select(value => value.Value).ToArray();
            var scopes = traits.Where(value => value.Key == "CudaEvidence").Select(value => value.Value).ToArray();
            if (categories.Contains("Cuda", StringComparer.OrdinalIgnoreCase) && scopes.Length != 1)
            {
                throw new InvalidOperationException("CUDA case lacks one scope trait: " + item.DisplayName);
            }

            Cases.Add(new Case(item.Id.ToString(), item.DisplayName, item.FullyQualifiedName, categories, scopes.SingleOrDefault()));
        }
    }
}
