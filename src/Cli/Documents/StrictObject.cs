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
        CheckNoDuplicateFields();
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
            var name = NameOf(property, Path);
            if (!_seen.Contains(name))
            {
                throw new InputException($"unknown field '{name}' at {Path}");
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

    public static string AsString(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InputException($"expected a string at {path}, not {Describe(value)}");
        }

        try
        {
            return value.GetString()!;
        }
        catch (InvalidOperationException inner)
        {
            throw new InputException($"the string at {path} is not valid UTF-16 text", inner);
        }
    }

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
            var name = NameOf(property, path);
            if (!map.TryAdd(name, AsNumber(property.Value, $"{path}.{name}")))
            {
                throw new InputException($"the key '{name}' at {path} is given twice");
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

    /// <summary>
    /// A member given twice is refused instead of letting the last value win silently (2026-09-26, the audit's
    /// finding 2): <see cref="JsonElement.EnumerateObject"/> yields every member in file order, duplicates included,
    /// while <see cref="JsonElement.TryGetProperty(string, out JsonElement)"/> would quietly resolve to the last one.
    /// Every object is constructed through this check first, so it is also the earliest point a lone UTF-16 surrogate
    /// in a member name is read (<see cref="NameOf"/>, the second hidden-defect audit of 2026-09-28, finding F6).
    /// </summary>
    private void CheckNoDuplicateFields()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in _element.EnumerateObject())
        {
            var name = NameOf(property, Path);
            if (!names.Add(name))
            {
                throw new InputException($"the field '{name}' at {Path} is given twice");
            }
        }
    }

    /// <summary>
    /// A member name, refused as an <see cref="InputException"/> naming <paramref name="context"/> (the object or
    /// map it belongs to) when it is not valid UTF-16 text (a lone surrogate, the second hidden-defect audit of
    /// 2026-09-28, finding F6): unescaping such a name throws <see cref="InvalidOperationException"/>, which no
    /// reader used to catch, so it reached the caller as an unhandled exit 3.
    /// </summary>
    private static string NameOf(JsonProperty property, string context)
    {
        try
        {
            return property.Name;
        }
        catch (InvalidOperationException inner)
        {
            throw new InputException($"a member name at {context} is not valid UTF-16 text", inner);
        }
    }

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
