using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Usage: Sts2CompatCheck <sts2.dll> <patch-targets.txt> <mod.dll> [<mod.dll> ...]
// Exit codes: 0 = everything resolves, 1 = something is missing, 2 = bad arguments.
if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: Sts2CompatCheck <sts2.dll> <patch-targets.txt> <mod.dll> [<mod.dll> ...]");
    return 2;
}

using var sts2Pe = new PEReader(File.OpenRead(args[0]));
var peReaders = new List<PEReader> { sts2Pe };
string? godotPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])) ?? ".", "GodotSharp.dll");
if (File.Exists(godotPath)) peReaders.Add(new PEReader(File.OpenRead(godotPath)));
var game = new GameIndex(peReaders.Select(p => p.GetMetadataReader()).ToArray());
int problems = 0;

Console.WriteLine("=== string-based patch targets");
foreach (string line in File.ReadAllLines(args[1]))
{
    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2 || parts[0].StartsWith('#')) continue;
    problems += Report(game.CheckMember(parts[0], parts[1]), $"{parts[0]}::{parts[1]}");
}

foreach (string modPath in args.Skip(2))
{
    Console.WriteLine($"=== compile-time references in {Path.GetFileName(modPath)}");
    using var pe = new PEReader(File.OpenRead(modPath));
    var mod = pe.GetMetadataReader();

    foreach (var handle in mod.TypeReferences)
    {
        string? name = Sts2TypeName(mod, handle);
        if (name != null) problems += Report(game.CheckType(name), name);
    }

    foreach (var handle in mod.MemberReferences)
    {
        var member = mod.GetMemberReference(handle);
        string? typeName = Sts2TypeName(mod, member.Parent);
        if (typeName == null) continue;
        string memberName = mod.GetString(member.Name);
        problems += Report(game.CheckMember(typeName, memberName), $"{typeName}::{memberName}");
    }
}

Console.WriteLine(problems == 0
    ? "OK: every referenced game type and member exists in this sts2.dll."
    : string.Create(CultureInfo.InvariantCulture, $"FAILED: {problems} missing reference(s)."));
return problems == 0 ? 0 : 1;

static int Report(Check result, string what)
{
    switch (result)
    {
        case Check.Ok:
            return 0;
        case Check.InheritedOutsideGame:
            Console.WriteLine($"  note: {what} is inherited from outside sts2.dll (not verified)");
            return 0;
        default:
            Console.WriteLine($"  MISSING {what}");
            return 1;
    }
}

static string? Sts2TypeName(MetadataReader reader, EntityHandle handle)
{
    if (handle.Kind != HandleKind.TypeReference) return null;
    var typeRef = reader.GetTypeReference((TypeReferenceHandle)handle);
    if (typeRef.ResolutionScope.Kind != HandleKind.AssemblyReference) return null;
    var assembly = reader.GetAssemblyReference((AssemblyReferenceHandle)typeRef.ResolutionScope);
    if (reader.GetString(assembly.Name) != "sts2") return null;
    return reader.GetString(typeRef.Namespace) + "." + reader.GetString(typeRef.Name);
}

internal enum Check
{
    Ok,
    InheritedOutsideGame,
    Missing
}

internal sealed class GameIndex
{
    private readonly Dictionary<string, (MetadataReader Reader, TypeDefinitionHandle Handle)> _types = new(StringComparer.Ordinal);

    // The first reader is the game assembly; the rest (GodotSharp) only resolve inherited members.
    public GameIndex(MetadataReader[] readers)
    {
        foreach (var reader in readers)
        {
            foreach (var handle in reader.TypeDefinitions)
            {
                _types.TryAdd(FullName(reader, reader.GetTypeDefinition(handle)), (reader, handle));
            }
        }

        GameReader = readers[0];
    }

    private MetadataReader GameReader { get; }

    public Check CheckType(string fullName) =>
        _types.TryGetValue(fullName, out var entry) && entry.Reader == GameReader ? Check.Ok : Check.Missing;

    public Check CheckMember(string typeName, string memberName)
    {
        if (!_types.TryGetValue(typeName, out var entry) || entry.Reader != GameReader) return Check.Missing;
        return Find(entry.Reader, entry.Handle, memberName);
    }

    private Check Find(MetadataReader reader, TypeDefinitionHandle handle, string memberName)
    {
        var type = reader.GetTypeDefinition(handle);
        foreach (var method in type.GetMethods())
        {
            if (reader.GetString(reader.GetMethodDefinition(method).Name) == memberName) return Check.Ok;
        }

        foreach (var field in type.GetFields())
        {
            if (reader.GetString(reader.GetFieldDefinition(field).Name) == memberName) return Check.Ok;
        }

        foreach (var property in type.GetProperties())
        {
            if (reader.GetString(reader.GetPropertyDefinition(property).Name) == memberName) return Check.Ok;
        }

        var baseType = type.BaseType;
        if (baseType.IsNil) return Check.Missing;
        if (baseType.Kind == HandleKind.TypeDefinition) return Find(reader, (TypeDefinitionHandle)baseType, memberName);
        if (baseType.Kind == HandleKind.TypeReference)
        {
            var reference = reader.GetTypeReference((TypeReferenceHandle)baseType);
            string baseName = reader.GetString(reference.Namespace) + "." + reader.GetString(reference.Name);
            if (_types.TryGetValue(baseName, out var resolved)) return Find(resolved.Reader, resolved.Handle, memberName);
            if (baseName == "System.Object") return Check.Missing;
        }

        // Base lives in the runtime libraries (System.Object etc.) or is generic: cannot verify by name.
        return Check.InheritedOutsideGame;
    }

    private static string FullName(MetadataReader reader, TypeDefinition type)
    {
        string name = reader.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        if (!declaring.IsNil) return FullName(reader, reader.GetTypeDefinition(declaring)) + "/" + name;
        string ns = reader.GetString(type.Namespace);
        return ns.Length > 0 ? ns + "." + name : name;
    }
}
