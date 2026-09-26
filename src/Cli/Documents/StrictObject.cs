using System.Text.Json;

namespace APThermo.Cli.Documents;

/// <summary>A JSON object read strictly: every field is known, every value has the expected type, and the path names the offender.</summary>
internal sealed class StrictObject
{
    private readonly JsonElement _element;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    public StrictObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InputException($"expected an object at {path}, not {Describe(element)}");
        }

        _element = element;
        Path = path;
    }

    public string Path { get; }

    public bool Has(string name) => _element.TryGetProperty(name, out _);

    public double Number(string name) => AsNumber(Required(name), PathOf(name));

    public double? OptionalNumber(string name) => Take(name) is { } value ? AsNumber(value, PathOf(name)) : null;

    public string String(string name) => AsString(Required(name), PathOf(name));

    public string? OptionalString(string name) => Take(name) is { } value ? AsString(value, PathOf(name)) : null;

    public bool OptionalBool(string name, bool fallback) =>
        Take(name) is not { } value
            ? fallback
            : value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Undefined or JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.String or JsonValueKind.Number or JsonValueKind.Null =>
                    throw new InputException($"expected true or false at {PathOf(name)}, not {Describe(value)}"),
                _ => throw new InputException($"expected true or false at {PathOf(name)}, not {Describe(value)}"),
            };

    public StrictObject Object(string name) => new(Required(name), PathOf(name));

    public StrictObject? OptionalObject(string name) => Take(name) is { } value ? new StrictObject(value, PathOf(name)) : null;

    public IReadOnlyList<StrictObject> ObjectList(string name)
    {
        var value = Required(name);
        var path = PathOf(name);
        return value.ValueKind != JsonValueKind.Array
            ? throw new InputException($"expected an array at {path}, not {Describe(value)}")
            : [.. value.EnumerateArray().Select((item, i) => new StrictObject(item, $"{path}[{i}]"))];
    }

    public IReadOnlyList<double>? OptionalNumberList(string name) => Take(name) is { } value ? AsNumberList(value, PathOf(name)) : null;

    public IReadOnlyList<string>? OptionalStringList(string name)
    {
        if (Take(name) is not { } value)
        {
            return null;
        }

        var path = PathOf(name);
        return value.ValueKind != JsonValueKind.Array
            ? throw new InputException($"expected an array of strings at {path}, not {Describe(value)}")
            : [.. value.EnumerateArray().Select((item, i) => AsString(item, $"{path}[{i}]"))];
    }

    public IReadOnlyDictionary<string, double> NumberMap(string name) => AsNumberMap(Required(name), PathOf(name));

    public IReadOnlyDictionary<string, double>? OptionalNumberMap(string name) => Take(name) is { } value ? AsNumberMap(value, PathOf(name)) : null;

    /// <summary>A field the caller interprets itself (a list or a range); consumed here so that it is not reported unknown.</summary>
    public JsonElement? OptionalAny(string name) => Take(name);

    /// <summary>Every field of the object must have been read: a field nobody asked for is a misspelling or a unit nobody agreed on.</summary>
    public void Finish()
    {
        foreach (var property in _element.EnumerateObject())
        {
            if (!_seen.Contains(property.Name))
            {
                throw new InputException($"unknown field '{property.Name}' at {Path}");
            }
        }
    }

    public static double AsNumber(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new InputException($"expected a number at {path}, not {Describe(value)}");
        }

        var number = value.GetDouble();
        return double.IsFinite(number) ? number : throw new InputException($"the number at {path} is out of range");
    }

    public static string AsString(JsonElement value, string path) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new InputException($"expected a string at {path}, not {Describe(value)}");

    public static IReadOnlyList<double> AsNumberList(JsonElement value, string path) =>
        value.ValueKind != JsonValueKind.Array
            ? throw new InputException($"expected an array of numbers at {path}, not {Describe(value)}")
            : [.. value.EnumerateArray().Select((item, i) => AsNumber(item, $"{path}[{i}]"))];

    public static IReadOnlyDictionary<string, double> AsNumberMap(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InputException($"expected an object of numbers at {path}, not {Describe(value)}");
        }

        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!map.TryAdd(property.Name, AsNumber(property.Value, $"{path}.{property.Name}")))
            {
                throw new InputException($"the key '{property.Name}' at {path} is given twice");
            }
        }

        return map;
    }

    public static string Describe(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Null => "null",
        JsonValueKind.Undefined => "nothing",
        _ => "nothing",
    };

    private JsonElement Required(string name) => Take(name) ?? throw new InputException($"missing field '{name}' at {Path}");

    private JsonElement? Take(string name)
    {
        if (_element.TryGetProperty(name, out var value))
        {
            _ = _seen.Add(name);
            return value;
        }

        return null;
    }

    private string PathOf(string name) => $"{Path}.{name}";
}
