using System.Text.Json;

namespace APThermo.Harness;

/// <summary>
/// The part of JSON Schema the command line's schema files use: type, enum, const, properties, required, additionalProperties,
/// items, minItems, minimum, exclusiveMinimum, oneOf, anyOf, not and local $ref into $defs. Enough to hold the documented shapes
/// without a dependency; a keyword outside this list is an error, so a schema cannot silently ask for more than is checked.
/// </summary>
public sealed class JsonSchema
{
    private static readonly HashSet<string> Known =
    [
        "$schema", "$id", "title", "description", "$defs", "$ref", "type", "enum", "const", "properties", "required",
        "additionalProperties", "items", "minItems", "minimum", "exclusiveMinimum", "oneOf", "anyOf", "not",
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
        var schema = new JsonSchema(document.RootElement.Clone());
        schema.Audit(schema._root, new HashSet<string>(StringComparer.Ordinal));
        return schema;
    }

    /// <summary>Every violation, with its path; empty when the instance conforms.</summary>
    public IReadOnlyList<string> Validate(JsonElement instance)
    {
        var errors = new List<string>();
        Check(_root, instance, "$", errors);
        return errors;
    }

    /// <summary>One schema box against one instance, split by the kind of check ($ref siblings combine; type gates kind-specific checks).</summary>
    private void Check(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            if (schema.ValueKind == JsonValueKind.False)
            {
                errors.Add($"{path}: the false schema rejects every instance");
            }
            return;
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            Check(Resolve(reference.GetString()!), instance, path, errors);
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

    /// <summary>Audit the supported finite schema graph before any particular instance can hide a branch.</summary>
    private void Audit(JsonElement schema, HashSet<string> references)
    {
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return;
        }
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("a schema box must be an object or a boolean");
        }
        CheckKeywords(schema);
        CheckNumericKeywords(schema);
        AuditType(schema);
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var pointer = reference.ValueKind == JsonValueKind.String ? reference.GetString() : null;
            if (string.IsNullOrEmpty(pointer))
            {
                throw new InvalidOperationException("a schema reference must be a non-empty string");
            }
            if (!references.Add(pointer))
            {
                throw new InvalidOperationException($"cyclic schema reference '{pointer}' is not supported");
            }
            Audit(Resolve(pointer), references);
            _ = references.Remove(pointer);
        }
        AuditMap(schema, "$defs", references);
        AuditMap(schema, "properties", references);
        AuditSingle(schema, "additionalProperties", references);
        AuditSingle(schema, "items", references);
        AuditSingle(schema, "not", references);
        AuditAlternatives(schema, "oneOf", references);
        AuditAlternatives(schema, "anyOf", references);
    }

    private void AuditMap(JsonElement schema, string keyword, HashSet<string> references)
    {
        if (!schema.TryGetProperty(keyword, out var map))
        {
            return;
        }
        if (map.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"schema '{keyword}' must be an object");
        }
        foreach (var property in map.EnumerateObject())
        {
            Audit(property.Value, references);
        }
    }

    private void AuditSingle(JsonElement schema, string keyword, HashSet<string> references)
    {
        if (schema.TryGetProperty(keyword, out var child))
        {
            Audit(child, references);
        }
    }

    private void AuditAlternatives(JsonElement schema, string keyword, HashSet<string> references)
    {
        if (!schema.TryGetProperty(keyword, out var alternatives))
        {
            return;
        }
        if (alternatives.ValueKind != JsonValueKind.Array || alternatives.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"schema '{keyword}' must be a non-empty array");
        }
        foreach (var alternative in alternatives.EnumerateArray())
        {
            Audit(alternative, references);
        }
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

    private static void CheckNumericKeywords(JsonElement schema)
    {
        foreach (var keyword in new[] { "minimum", "exclusiveMinimum", "minItems" })
        {
            if (!schema.TryGetProperty(keyword, out var value))
            {
                continue;
            }
            var number = ExactJsonNumber.From(value);
            if (keyword == "minItems" && (!number.IsInteger || number.Compare(ExactJsonNumber.FromInt32(0)) < 0))
            {
                throw new InvalidOperationException("schema minItems must be a nonnegative integer");
            }
        }
    }

    private static void AuditType(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return;
        }
        var types = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().ToArray() : [type];
        if (types.Length == 0 || types.Any(t => t.ValueKind != JsonValueKind.String ||
            t.GetString() is not ("object" or "array" or "string" or "number" or "integer" or "boolean" or "null")) ||
            types.Select(t => t.GetString()).Distinct(StringComparer.Ordinal).Count() != types.Length)
        {
            throw new InvalidOperationException("schema type must name distinct supported types");
        }
    }

    /// <summary>type: false stops every other check on this instance, as a type mismatch makes them meaningless.</summary>
    private static bool CheckType(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (schema.TryGetProperty("type", out var type) && !TypeMatches(type, instance))
        {
            errors.Add($"{path}: expected {type}, found {Kind(instance)}");
            return false;
        }

        return true;
    }

    /// <summary>enum, const: checked against the instance's value regardless of its kind.</summary>
    private static void CheckValue(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (schema.TryGetProperty("enum", out var allowed) && !allowed.EnumerateArray().Any(v => JsonValueEquality.Equal(v, instance)))
        {
            errors.Add($"{path}: {instance} is not one of {allowed}");
        }

        if (schema.TryGetProperty("const", out var constant) && !JsonValueEquality.Equal(constant, instance))
        {
            errors.Add($"{path}: expected {constant}, found {instance}");
        }
    }

    private static void CheckNumber(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (instance.ValueKind != JsonValueKind.Number)
        {
            return;
        }

        var value = ExactJsonNumber.From(instance);
        if (schema.TryGetProperty("minimum", out var minimum) && value.Compare(ExactJsonNumber.From(minimum)) < 0)
        {
            errors.Add($"{path}: {instance} is below the minimum {minimum}");
        }

        if (schema.TryGetProperty("exclusiveMinimum", out var exclusive) && value.Compare(ExactJsonNumber.From(exclusive)) <= 0)
        {
            errors.Add($"{path}: {instance} is not above {exclusive}");
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
        if (schema.TryGetProperty("minItems", out var minItems) && ExactJsonNumber.FromInt32(count).Compare(ExactJsonNumber.From(minItems)) < 0)
        {
            errors.Add($"{path}: {count} items, fewer than {minItems}");
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

        if (schema.TryGetProperty("not", out var excluded) && Conforms(excluded, instance))
        {
            errors.Add($"{path}: matches the excluded schema");
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
        var element = _root;
        foreach (var name in JsonPointerFragment.Tokens(reference))
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
            {
                throw new InvalidOperationException($"the reference '{reference}' does not resolve");
            }
        }

        return element;
    }

    private static bool TypeMatches(JsonElement type, JsonElement instance) =>
        type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Any(t => TypeMatches(t, instance))
            : type.GetString() switch
            {
                "object" => instance.ValueKind == JsonValueKind.Object,
                "array" => instance.ValueKind == JsonValueKind.Array,
                "string" => instance.ValueKind == JsonValueKind.String,
                "number" => instance.ValueKind == JsonValueKind.Number,
                "integer" => instance.ValueKind == JsonValueKind.Number && ExactJsonNumber.From(instance).IsInteger,
                "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "null" => instance.ValueKind == JsonValueKind.Null,
                var other => throw new InvalidOperationException($"unknown type '{other}' in the schema"),
            };

    private static string Kind(JsonElement instance) => instance.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.Null => "null",
        JsonValueKind.Undefined => "undefined",
        _ => throw new ArgumentOutOfRangeException(nameof(instance), instance.ValueKind, "unknown JSON value kind"),
    };
}
