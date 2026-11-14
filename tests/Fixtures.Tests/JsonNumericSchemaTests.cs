using System.Globalization;
using System.Text.Json;
using APThermo.Harness;

namespace APThermo.Fixtures.Tests;

/// <summary>Whole-schema mathematical decimal controls, including values beyond machine numeric ranges.</summary>
public sealed class JsonNumericSchemaTests
{
    /// <summary>Integer means zero mathematical fractional part, independent of token spelling and binary64 rounding.</summary>
    [Theory]
    [InlineData("1.0000000000000001", false)]
    [InlineData("-1.0000000000000001", false)]
    [InlineData("1.0000000000000000", true)]
    [InlineData("1e0", true)]
    [InlineData("-1234500E+0002", true)]
    [InlineData("12345E-0002", false)]
    [InlineData("1e-400", false)]
    [InlineData("1000e-3", true)]
    [InlineData("1000e-4", false)]
    [InlineData("0e-999999999999999999999999999999", true)]
    [InlineData("-0.0000e-92233720368547758081234567890", true)]
    [InlineData("1e2147483648", true)]
    [InlineData("1e-2147483648", false)]
    [InlineData("-2.5", false)]
    [InlineData("-1e999999999999999999999999999", true)]
    [InlineData("100.001000e3", true)]
    [InlineData("0.00000000000000000001e20", true)]
    public void IntegerUsesTheExactDecimalValue(string instance, bool conforms) =>
        AssertConforms("""{"type":"integer"}""", instance, conforms);

    /// <summary>Bounds compare exact signed values without overflow, underflow or lost low digits.</summary>
    [Theory]
    [InlineData("minimum", "9007199254740993", "9007199254740992", false)]
    [InlineData("minimum", "9007199254740993", "9007199254740993", true)]
    [InlineData("exclusiveMinimum", "9007199254740992", "9007199254740993", true)]
    [InlineData("minimum", "1e-400", "0", false)]
    [InlineData("minimum", "-1e-400", "0", true)]
    [InlineData("exclusiveMinimum", "-1e-400", "0", true)]
    [InlineData("exclusiveMinimum", "-1e-400", "-1e-400", false)]
    [InlineData("minimum", "-9007199254740992", "-9007199254740993", false)]
    [InlineData("minimum", "1e400", "9e399", false)]
    [InlineData("exclusiveMinimum", "1e400", "1e401", true)]
    [InlineData("minimum", "1e400", "10e399", true)]
    [InlineData("minimum", "1e999999999999999999999999999", "9e999999999999999999999999998", false)]
    [InlineData("minimum", "1e999999999999999999999999999", "10e999999999999999999999999998", true)]
    [InlineData("exclusiveMinimum", "1e999999999999999999999999999", "10e999999999999999999999999998", false)]
    [InlineData("minimum", "-9e999999999999999999999999998", "-1e999999999999999999999999999", false)]
    [InlineData("minimum", "1e-999999999999999999999", "9e-1000000000000000000000", false)]
    [InlineData("minimum", "1.2345678901234567890123456789", "1.2345678901234567890123456788", false)]
    public void BoundsUseExactDecimalOrder(string keyword, string bound, string instance, bool conforms) =>
        AssertConforms("{\"" + keyword + "\":" + bound + "}", instance, conforms);

    /// <summary>Enum/const preserve numeric value equality recursively and distinguish different kinds/orders.</summary>
    [Theory]
    [InlineData("{\"const\":1}", "1.0", true)]
    [InlineData("{\"enum\":[1]}", "1e0", true)]
    [InlineData("{\"const\":1.0000000000000001}", "1", false)]
    [InlineData("{\"const\":9007199254740993}", "9007199254740992", false)]
    [InlineData("{\"const\":1e400}", "10e399", true)]
    [InlineData("{\"const\":1e400}", "11e399", false)]
    [InlineData("{\"enum\":[0]}", "1e-400", false)]
    [InlineData("{\"const\":-0.000e-99999999999999}", "0", true)]
    [InlineData("{\"const\":{\"a\":[1e400,1e-400]}}", "{\"a\":[10e399,10e-401]}", true)]
    [InlineData("{\"const\":{\"a\":[1e400]}}", "{\"a\":[-1e400]}", false)]
    [InlineData("{\"const\":{\"x\":1,\"y\":2}}", "{\"y\":2.0,\"x\":1e0}", true)]
    [InlineData("{\"enum\":[{\"x\":1e999999999999999999999999999}]}", "{\"x\":10e999999999999999999999999998}", true)]
    [InlineData("{\"const\":1}", "\"1\"", false)]
    [InlineData("{\"const\":[1,2]}", "[2,1]", false)]
    public void EnumAndConstUseExactJsonValueEquality(string schema, string instance, bool conforms) =>
        AssertConforms(schema, instance, conforms);

    /// <summary>Array bounds accept all valid integer spellings and do not overflow Int32.</summary>
    [Theory]
    [InlineData("1e0", "[]", false)]
    [InlineData("1.0", "[1]", true)]
    [InlineData("1e400", "[]", false)]
    [InlineData("1e0", "[1]", true)]
    [InlineData("9007199254740993", "[]", false)]
    [InlineData("0e-999999999999999999999", "[]", true)]
    [InlineData("3", "[1,2,3]", true)]
    public void ItemCountBoundsUseExactIntegerValues(string minimum, string instance, bool conforms) =>
        AssertConforms("{\"minItems\":" + minimum + "}", instance, conforms);

    /// <summary>Invalid numeric keyword values fail before an unused branch could hide them.</summary>
    [Theory]
    [InlineData("{\"minimum\":\"1\"}")]
    [InlineData("{\"exclusiveMinimum\":null}")]
    [InlineData("{\"minItems\":-1}")]
    [InlineData("{\"minItems\":1.5}")]
    [InlineData("{\"properties\":{\"unused\":{\"minItems\":0.5}}}")]
    public void InvalidNumericKeywordsFailDuringSchemaAudit(string text) =>
        _ = Assert.Throws<InvalidOperationException>(() => JsonSchema.Parse(text));

    /// <summary>An independent BCL decimal oracle checks the generated bounded coefficient/exponent matrix.</summary>
    [Fact]
    public void BoundedValuesAgreeWithAnIndependentDecimalOracle()
    {
        int[] coefficients = [-12345, -100, -1, 0, 1, 100, 12345];
        var tokens = coefficients.SelectMany(coefficient => Enumerable.Range(-5, 11)
            .Select(exponent => coefficient.ToString(CultureInfo.InvariantCulture) + "e" + exponent.ToString(CultureInfo.InvariantCulture)))
            .ToArray();
        Assert.NotEmpty(tokens);
        foreach (var bound in tokens)
        {
            var schema = JsonSchema.Parse("{\"minimum\":" + bound + "}");
            var expectedMinimum = decimal.Parse(bound, NumberStyles.Float, CultureInfo.InvariantCulture);
            foreach (var token in tokens)
            {
                using var instance = JsonDocument.Parse(token);
                var expected = decimal.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture) >= expectedMinimum;
                Assert.Equal(expected, schema.Validate(instance.RootElement).Count == 0);
            }
        }
    }

    private static void AssertConforms(string schemaText, string instanceText, bool conforms)
    {
        var schema = JsonSchema.Parse(schemaText);
        using var instance = JsonDocument.Parse(instanceText);
        var problems = schema.Validate(instance.RootElement);
        Assert.Equal(conforms, problems.Count == 0);
        if (!conforms)
        {
            Assert.All(problems, problem => Assert.StartsWith("$", problem, StringComparison.Ordinal));
        }
    }
}
