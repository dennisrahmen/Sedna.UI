using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Sedna.UI.Catalogue.Mcp;

/// <summary>One public C# member, or a type, as <c>describe_api</c> reports it.</summary>
internal sealed record ApiMember(
    string Name,
    string Kind,
    IReadOnlyList<string> Signatures,
    string? Summary,
    IReadOnlyDictionary<string, string> Parameters,
    string? Returns);

/// <summary>One member of the <c>sednaUi</c> global, from the table in <c>docs/architecture.md</c>.</summary>
internal sealed record ScriptMember(IReadOnlyList<string> Names, string Purpose);

/// <summary>
/// The library's public API, member by member: the C# surface read by reflection from the
/// assembly the app references, with each member's documentation from the XML file the
/// library's build writes beside it, and the <c>sednaUi</c> global from the member table in
/// <c>docs/architecture.md</c>.
/// </summary>
/// <remarks>
/// Nothing is hand-listed. The C# names are the same set <see cref="CatalogueIndex.PublicApi"/>
/// holds, and the script's are the rows of the table <c>SurfaceTests</c> pins — the table is
/// the documentation of that surface, so describing a member from anywhere else could only
/// disagree with it.
/// </remarks>
internal sealed class ApiReference
{
    private readonly Dictionary<string, XElement> _docs;

    public ApiReference()
    {
        _docs = ReadXmlDocs();
        Types = typeof(ISednaUi).Assembly.GetExportedTypes()
            .OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        Script = ReadScriptTable(Docs.Read("architecture.md"));
    }

    /// <summary>The exported types.</summary>
    public IReadOnlyList<Type> Types { get; }

    /// <summary>The rows of the <c>sednaUi</c> member table that name a member.</summary>
    public IReadOnlyList<ScriptMember> Script { get; }

    /// <summary>Whether the XML documentation file was found beside the assembly.</summary>
    public bool HasDocs => _docs.Count > 0;

    /// <summary>
    /// A type and its members, or every member of that name on every exported type:
    /// <c>ISednaUi</c>, <c>ISednaUi.ShowModalAsync</c> or a bare <c>ShowModalAsync</c>.
    /// </summary>
    public IReadOnlyList<ApiMember> Csharp(string name)
    {
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        var typeName = dot < 0 ? name : name[..dot];
        var memberName = dot < 0 ? null : name[(dot + 1)..];

        var type = Types.FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.Ordinal));
        if (type is not null && memberName is null) return [Describe(type)];
        if (type is not null) return Members(type, memberName!);

        // A bare member name: every type that declares it.
        return dot < 0 ? Types.SelectMany(t => Members(t, name)).ToList() : [];
    }

    /// <summary>The script table's row for <c>sednaUi.toast</c>, <c>toast</c> or
    /// <c>toast.show</c> — a function on a member is described by its member's row.</summary>
    public ScriptMember? ScriptRow(string name)
    {
        var bare = name.StartsWith("sednaUi.", StringComparison.Ordinal) ? name["sednaUi.".Length..] : name;
        var head = bare.Split('.')[0].Split('(')[0];
        return Script.FirstOrDefault(r => r.Names.Contains(head, StringComparer.Ordinal));
    }

    /// <summary>Every name a reader could ask for, for the nearest-match answer.</summary>
    public IEnumerable<string> AllNames() =>
        Types.Select(t => t.Name)
            .Concat(Types.SelectMany(t => DeclaredMembers(t).Select(m => $"{t.Name}.{m.Name}")))
            .Concat(Script.SelectMany(r => r.Names).Select(n => "sednaUi." + n))
            .Distinct(StringComparer.Ordinal);

    private List<ApiMember> Members(Type type, string memberName)
    {
        var members = DeclaredMembers(type)
            .Where(m => string.Equals(m.Name, memberName, StringComparison.Ordinal))
            .ToList();
        if (members.Count == 0) return [];

        var first = members[0];
        var doc = Doc(first);
        return
        [
            new ApiMember(
                $"{type.Name}.{memberName}",
                Kind(first),
                members.Select(Signature).Distinct(StringComparer.Ordinal).ToList(),
                Text(doc?.Element("summary")),
                Params(doc),
                Text(doc?.Element("returns"))),
        ];
    }

    private ApiMember Describe(Type type)
    {
        var doc = _docs.GetValueOrDefault("T:" + type.FullName);
        var kind = type.IsInterface ? "interface" : type.IsEnum ? "enum" : type.IsValueType ? "struct"
            : type.IsAbstract && type.IsSealed ? "static class" : IsRecord(type) ? "record" : "class";
        var signatures = DeclaredMembers(type).Select(Signature).Distinct(StringComparer.Ordinal).ToList();
        return new ApiMember(type.Name, kind, signatures, Text(doc?.Element("summary")),
            new Dictionary<string, string>(), null);
    }

    private static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null;

    /// <summary>What an app writes: no accessors, no compiler-written members.</summary>
    private static IEnumerable<MemberInfo> DeclaredMembers(Type type) =>
        type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m is not MethodBase { IsSpecialName: true } || m is ConstructorInfo)
            .Where(m => m is not ConstructorInfo || !type.IsInterface)
            .Where(m => !m.Name.StartsWith('<') && !Synthesised.Contains(m.Name))
            .Where(m => m is not ConstructorInfo c || c.GetParameters().Length > 0 || !IsRecord(type))
            .OrderBy(m => m.Name, StringComparer.Ordinal);

    private static readonly HashSet<string> Synthesised = new(StringComparer.Ordinal)
    {
        "Equals", "GetHashCode", "ToString", "Deconstruct", "PrintMembers", "EqualityContract",
        "GetType", "Clone", "value__",
    };

    private static string Kind(MemberInfo member) => member switch
    {
        MethodInfo => "method",
        PropertyInfo => "property",
        FieldInfo f when f.DeclaringType?.IsEnum == true => "enum value",
        FieldInfo => "field",
        EventInfo => "event",
        ConstructorInfo => "constructor",
        _ => "member",
    };

    // ── Signatures ──────────────────────────────────────────────────────────

    private static readonly NullabilityInfoContext Nullability = new();

    private static string Signature(MemberInfo member) => member switch
    {
        MethodInfo m => $"{(m.IsStatic ? "static " : "")}{TypeName(m.ReturnType, Nullability.Create(m.ReturnParameter))} "
                        + $"{m.Name}{Generics(m)}({string.Join(", ", m.GetParameters().Select(Parameter))})",
        ConstructorInfo c => $"{c.DeclaringType!.Name}({string.Join(", ", c.GetParameters().Select(Parameter))})",
        PropertyInfo p => $"{TypeName(p.PropertyType, Nullability.Create(p))} {p.Name} {{ {Accessors(p)} }}",
        FieldInfo f when f.DeclaringType?.IsEnum == true => f.Name,
        FieldInfo f => $"{(f.IsStatic ? "static " : "")}{TypeName(f.FieldType, Nullability.Create(f))} {f.Name}",
        EventInfo e => $"event {TypeName(e.EventHandlerType!, null)} {e.Name}",
        _ => member.Name,
    };

    private static string Accessors(PropertyInfo p) =>
        string.Join(" ", new[]
        {
            p.GetMethod is { IsPublic: true } ? "get;" : null,
            p.SetMethod is { IsPublic: true } set
                ? set.ReturnParameter.GetRequiredCustomModifiers().Any(t => t.Name == "IsExternalInit") ? "init;" : "set;"
                : null,
        }.Where(a => a is not null));

    private static string Generics(MethodInfo m) =>
        m.IsGenericMethodDefinition ? $"<{string.Join(", ", m.GetGenericArguments().Select(a => a.Name))}>" : "";

    private static string Parameter(ParameterInfo p)
    {
        var text = $"{TypeName(p.ParameterType, Nullability.Create(p))} {p.Name}";
        if (p.HasDefaultValue)
            text += " = " + p.DefaultValue switch
            {
                null => p.ParameterType.IsValueType && Nullable.GetUnderlyingType(p.ParameterType) is null ? "default" : "null",
                string s => $"\"{s}\"",
                bool b => b ? "true" : "false",
                var v when p.ParameterType.IsEnum => $"{p.ParameterType.Name}.{v}",
                var v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture),
            };
        return text;
    }

    private static readonly Dictionary<Type, string> Keywords = new()
    {
        [typeof(void)] = "void", [typeof(string)] = "string", [typeof(bool)] = "bool", [typeof(int)] = "int",
        [typeof(long)] = "long", [typeof(double)] = "double", [typeof(float)] = "float", [typeof(decimal)] = "decimal",
        [typeof(object)] = "object", [typeof(byte)] = "byte",
    };

    private static string TypeName(Type type, NullabilityInfo? nullability)
    {
        if (type.IsByRef) type = type.GetElementType()!;
        var nullableValue = Nullable.GetUnderlyingType(type);
        if (nullableValue is not null) return TypeName(nullableValue, null) + "?";

        string name;
        if (Keywords.TryGetValue(type, out var keyword)) name = keyword;
        else if (type.IsArray) name = TypeName(type.GetElementType()!, nullability?.ElementType) + "[]";
        else if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            var argInfo = nullability?.GenericTypeArguments;
            name = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)] + "<" + string.Join(", ",
                args.Select((a, i) => TypeName(a, argInfo is not null && i < argInfo.Length ? argInfo[i] : null))) + ">";
        }
        else name = type.Name;

        return !type.IsValueType && nullability?.ReadState == NullabilityState.Nullable ? name + "?" : name;
    }

    // ── XML documentation ───────────────────────────────────────────────────

    private XElement? Doc(MemberInfo member)
    {
        var type = member.DeclaringType!.FullName!.Replace('+', '.');
        var prefix = member switch
        {
            MethodInfo => "M:", ConstructorInfo => "M:", PropertyInfo => "P:", FieldInfo => "F:", EventInfo => "E:", _ => "",
        };
        var name = member is ConstructorInfo ? "#ctor" : member.Name;
        var id = $"{prefix}{type}.{name}";
        return _docs.GetValueOrDefault(id)
               ?? _docs.FirstOrDefault(d => d.Key.StartsWith(id + "(", StringComparison.Ordinal)
                                             || d.Key.StartsWith(id + "``", StringComparison.Ordinal)).Value;
    }

    private static IReadOnlyDictionary<string, string> Params(XElement? doc) =>
        doc?.Elements("param")
            .Where(p => p.Attribute("name") is not null)
            .ToDictionary(p => p.Attribute("name")!.Value, p => Text(p) ?? "", StringComparer.Ordinal)
        ?? new Dictionary<string, string>();

    /// <summary>A doc comment as plain text: a cross-reference becomes the name it points at.</summary>
    private static string? Text(XElement? element)
    {
        if (element is null) return null;
        var parts = element.Nodes().Select(node => node switch
        {
            XText t => t.Value,
            XElement { Name.LocalName: "see" or "seealso" } e =>
                e.Attribute("cref")?.Value is { } cref ? Short(cref)
                : e.Attribute("langword")?.Value ?? e.Value,
            XElement { Name.LocalName: "paramref" or "typeparamref" } e => e.Attribute("name")?.Value ?? "",
            XElement { Name.LocalName: "c" } e => $"`{e.Value}`",
            XElement { Name.LocalName: "para" } e => "\n\n" + Text(e) + "\n\n",
            XElement e => Text(e),
            _ => "",
        });
        var text = Regex.Replace(string.Concat(parts), @"[ \t\r]*\n[ \t\r]*\n\s*", "\n\n");
        text = Regex.Replace(text, @"[ \t\r]*\n(?!\n)[ \t\r]*", " ");
        text = Regex.Replace(text, @"[ \t]+", " ").Trim();
        return text.Length == 0 ? null : text;
    }

    private static string Short(string cref)
    {
        var name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        name = name.Split('(')[0].Replace("`", "", StringComparison.Ordinal);
        var parts = name.Split('.');
        return parts.Length >= 2 && cref.StartsWith("T:", StringComparison.Ordinal) ? parts[^1]
            : parts.Length >= 2 ? $"{parts[^2]}.{parts[^1]}" : name;
    }

    private static Dictionary<string, XElement> ReadXmlDocs()
    {
        var path = Path.ChangeExtension(typeof(ISednaUi).Assembly.Location, ".xml");
        if (!File.Exists(path)) return new Dictionary<string, XElement>(StringComparer.Ordinal);

        return XDocument.Load(path).Descendants("member")
            .Where(m => m.Attribute("name") is not null)
            .GroupBy(m => m.Attribute("name")!.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    // ── The script's member table ───────────────────────────────────────────

    private static readonly Regex Row = new(@"^\|\s*(?<names>[^|]+?)\s*\|\s*(?<purpose>.+?)\s*\|\s*$", RegexOptions.Multiline);
    private static readonly Regex Code = new(@"`(?<name>[a-zA-Z][a-zA-Z]*)(\([^`]*\))?`");

    internal static List<ScriptMember> ReadScriptTable(string architecture)
    {
        var start = architecture.IndexOf("exposes the global `sednaUi`", StringComparison.Ordinal);
        if (start < 0) return [];
        var end = architecture.IndexOf("\n\n", architecture.IndexOf("| Member |", start, StringComparison.Ordinal), StringComparison.Ordinal);
        var table = architecture[start..(end < 0 ? architecture.Length : end)];

        var rows = new List<ScriptMember>();
        foreach (Match row in Row.Matches(table))
        {
            var names = Code.Matches(row.Groups["names"].Value).Select(m => m.Groups["name"].Value).ToList();
            if (names.Count == 0) continue;
            rows.Add(new ScriptMember(names, row.Groups["purpose"].Value));
        }
        return rows;
    }
}
