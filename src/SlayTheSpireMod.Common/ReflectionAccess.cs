using System.Collections.Concurrent;
using System.Reflection;

namespace SlayTheSpireMod.Common;

/// <summary>
/// Name-based access to game internals. Mods cannot reference every game member at compile time
/// without breaking across game versions, so lookups are by string and return null when missing.
/// </summary>
public static class ReflectionAccess
{
    private const BindingFlags AnyDeclared =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(Type, string), MethodInfo?> MethodCache = new();
    private static readonly ConcurrentDictionary<(Type, string, bool), MemberInfo?> MemberCache = new();

    public static Type? GetCachedType(string fullName) =>
        TypeCache.GetOrAdd(fullName, static name =>
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name, throwOnError: false);
                if (type != null) return type;
            }
            return null;
        });

    public static MethodInfo? GetCachedMethod(Type type, string methodName) =>
        MethodCache.GetOrAdd((type, methodName), static key =>
        {
            for (var t = key.Item1; t != null; t = t.BaseType)
            {
                foreach (var method in t.GetMethods(AnyDeclared))
                {
                    if (method.Name == key.Item2) return method;
                }
            }
            return null;
        });

    /// <summary>Returns the first non-null value among the named instance properties/fields (property wins over field per name).</summary>
    public static object? GetMember(object? target, params string[] names)
    {
        if (target == null) return null;
        var type = target.GetType();
        foreach (string name in names)
        {
            var value = Read(Find(type, name, wantStatic: false), target);
            if (value != null) return value;
        }
        return null;
    }

    /// <summary>Returns the first non-null value among the named static properties/fields.</summary>
    public static object? GetStaticMember(Type type, params string[] names)
    {
        foreach (string name in names)
        {
            var value = Read(Find(type, name, wantStatic: true), null);
            if (value != null) return value;
        }
        return null;
    }

    private static MemberInfo? Find(Type type, string name, bool wantStatic) =>
        MemberCache.GetOrAdd((type, name, wantStatic), static key =>
        {
            for (var t = key.Item1; t != null; t = t.BaseType)
            {
                var property = t.GetProperty(key.Item2, AnyDeclared);
                if (property?.GetMethod != null && property.GetMethod.IsStatic == key.Item3) return property;
                var field = t.GetField(key.Item2, AnyDeclared);
                if (field != null && field.IsStatic == key.Item3) return field;
            }
            return null;
        });

    private static object? Read(MemberInfo? member, object? target) => member switch
    {
        PropertyInfo p => p.GetValue(target),
        FieldInfo f => f.GetValue(target),
        _ => null
    };
}
