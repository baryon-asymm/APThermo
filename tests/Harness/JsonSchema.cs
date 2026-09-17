using System.Text.Json;

namespace APThermo.Harness;

/// <summary>
/// The part of JSON Schema the command line's schema files use: type, enum, const, properties, required, additionalProperties,
/// items, minItems, minimum, exclusiveMinimum, oneOf, anyOf and local $ref into $defs. Enough to hold the documented shapes
/// without a dependency; a keyword outside this list is an error, so a schema cannot silently ask for more than is checked.
/// </summary>
public sealed class JsonSchema
{
    private static readonly HashSet<string> Known =
    [
        "$schema", "$id", "title", "description", "$defs", "$ref", "type", "enum", "const", "properties", "required",
        "additionalProperties", "items", "minItems", "minimum", "exclusiveMinimum", "oneOf", "anyOf",
    ];

    private readonly JsonElement _root;

    private JsonSchema(JsonElement root)
    {
        _root = root;
    }

    /// <summary>The schema's JSON text, as `apthermo schema` prints it (2026-09-16).</summary>
    public static JsonSchema Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        return new JsonSchema(document.RootElement.Clone());
    }

    /// <summary>Every violation, with its path; empty when the instance conforms.</summary>
    public IReadOnlyList<string> Validate(JsonElement instance)
    {
        var errors = new List<string>();
        Check(_root, instance, "$", errors);
        return errors;
    }

    /// <summary>One schema box against one instance, split by the kind of check ($ref and type gate the rest; the others by the instance's own kind).</summary>
    private void Check(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        CheckKeywords(schema);
        if (schema.TryGetProperty("$ref", out var reference))
        {
            Check(Resolve(reference.GetString()!), instance, path, errors);
            return;
        }

        if (!CheckType(schema, instance, path, errors))
        {
            return;
        }

        CheckValue(schema, instance, path, errors);
        CheckNumber(schema, instance, path, errors);
        CheckObject(schema, instance, path, errors);
        CheckArray(schema, instance, path, errors);
        CheckCombinators(schema, instance, path, errors);
    }

    /// <summary>A schema may not ask for a keyword this validator does not check (the keyword whitelist, unchanged).</summary>
    private static void CheckKeywords(JsonElement schema)
    {
        foreach (var keyword in schema.EnumerateObject())
        {
            if (!Known.Contains(keyword.Name))
            {
                throw new InvalidOperationException($"the schema uses the keyword '{keyword.Name}', which this validator does not check");
            }
        }
    }

    /// <summary>type: false stops every other check on this instance, as a type mismatch makes them meaningless.</summary>
    private static bool CheckType(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (schema.TryGetProperty("type", out var type) && !TypeMatches(type, instance))
        {
            errors.Add($"{path}: expected {type.ToString()}, found {Kind(instance)}");
            return false;
        }

        return true;
    }

    /// <summary>enum, const: checked against the instance's value regardless of its kind.</summary>
    private static void CheckValue(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (schema.TryGetProperty("enum", out var allowed) && !allowed.EnumerateArray().Any(v => JsonElement.DeepEquals(v, instance)))
        {
            errors.Add($"{path}: {instance.ToString()} is not one of {allowed.ToString()}");
        }

        if (schema.TryGetProperty("const", out var constant) && !JsonElement.DeepEquals(constant, instance))
        {
            errors.Add($"{path}: expected {constant.ToString()}, found {instance.ToString()}");
        }
    }

    private static void CheckNumber(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (instance.ValueKind != JsonValueKind.Number)
        {
            return;
        }

        var value = instance.GetDouble();
        if (schema.TryGetProperty("minimum", out var minimum) && value < minimum.GetDouble())
        {
            errors.Add($"{path}: {value} is below the minimum {minimum.GetDouble()}");
        }

        if (schema.TryGetProperty("exclusiveMinimum", out var exclusive) && value <= exclusive.GetDouble())
        {
            errors.Add($"{path}: {value} is not above {exclusive.GetDouble()}");
        }
    }

    private void CheckObject(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (instance.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var properties = schema.TryGetProperty("properties", out var p) ? p : default;
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var name in required.EnumerateArray())
            {
                if (!instance.TryGetProperty(name.GetString()!, out _))
                {
                    errors.Add($"{path}: missing '{name.GetString()}'");
                }
            }
        }

        foreach (var property in instance.EnumerateObject())
        {
            CheckProperty(schema, properties, property, path, errors);
        }
    }

    /// <summary>One property of an object instance: by its own schema when declared, else additionalProperties.</summary>
    private void CheckProperty(JsonElement schema, JsonElement properties, JsonProperty property, string path, List<string> errors)
    {
        var propertyPath = $"{path}.{property.Name}";
        if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out var propertySchema))
        {
            Check(propertySchema, property.Value, propertyPath, errors);
            return;
        }

        if (!schema.TryGetProperty("additionalProperties", out var additional))
        {
            return;
        }

        if (additional.ValueKind == JsonValueKind.False)
        {
            errors.Add($"{propertyPath}: not allowed");
        }
        else if (additional.ValueKind == JsonValueKind.Object)
        {
            Check(additional, property.Value, propertyPath, errors);
        }
    }

    private void CheckArray(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (instance.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var count = instance.GetArrayLength();
        if (schema.TryGetProperty("minItems", out var minItems) && count < minItems.GetInt32())
        {
            errors.Add($"{path}: {count} items, fewer than {minItems.GetInt32()}");
        }

        if (schema.TryGetProperty("items", out var items))
        {
            var i = 0;
            foreach (var item in instance.EnumerateArray())
            {
                Check(items, item, $"{path}[{i++}]", errors);
            }
        }
    }

    /// <summary>oneOf, anyOf: checked regardless of the instance's kind, each alternative a fresh, independent check.</summary>
    private void CheckCombinators(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (schema.TryGetProperty("oneOf", out var oneOf))
        {
            var matching = oneOf.EnumerateArray().Count(alternative => Conforms(alternative, instance));
            if (matching != 1)
            {
                errors.Add($"{path}: {matching} of the oneOf alternatives match, not exactly one");
            }
        }

        if (schema.TryGetProperty("anyOf", out var anyOf) && !anyOf.EnumerateArray().Any(alternative => Conforms(alternative, instance)))
        {
            errors.Add($"{path}: no anyOf alternative matches");
        }
    }

    private bool Conforms(JsonElement schema, JsonElement instance)
    {
        var errors = new List<string>();
        Check(schema, instance, "$", errors);
        return errors.Count == 0;
    }

    private JsonElement Resolve(string reference)
    {
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"only local references are supported, not '{reference}'");
        }

        var element = _root;
        foreach (var segment in reference[2..].Split('/'))
        {
            if (!element.TryGetProperty(segment, out element))
            {
                throw new InvalidOperationException($"the reference '{reference}' does not resolve");
            }
        }

        return element;
    }

    private static bool TypeMatches(JsonElement type, JsonElement instance)
    {
        if (type.ValueKind == JsonValueKind.Array)
        {
            return type.EnumerateArray().Any(t => TypeMatches(t, instance));
        }

        return type.GetString() switch
        {
            "object" => instance.ValueKind == JsonValueKind.Object,
            "array" => instance.ValueKind == JsonValueKind.Array,
            "string" => instance.ValueKind == JsonValueKind.String,
            "number" => instance.ValueKind == JsonValueKind.Number,
            "integer" => instance.ValueKind == JsonValueKind.Number && instance.GetDouble() == Math.Floor(instance.GetDouble()),
            "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "null" => instance.ValueKind == JsonValueKind.Null,
            var other => throw new InvalidOperationException($"unknown type '{other}' in the schema"),
        };
    }

    private static string Kind(JsonElement instance) => instance.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False => "boolean",
        var kind => kind.ToString().ToLowerInvariant(),
    };
}
