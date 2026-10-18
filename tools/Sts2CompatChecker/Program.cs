using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Sts2CompatChecker;

public static class Program
{
    private static readonly (string Type, string Member)[] StringPatchTargets =
    [
        // SlayTheSpireOverlay.Godot targets
        ("MegaCrit.Sts2.Core.Nodes.Cards.NCard", "UpdateVisuals"),
        ("MegaCrit.Sts2.Core.Nodes.Relics.NRelic", "Reload"),
        ("MegaCrit.Sts2.Core.Nodes.Potions.NPotion", "_Ready"),
        ("MegaCrit.Sts2.Core.Nodes.Potions.NPotion", "set_Model"),
        ("MegaCrit.Sts2.Core.Nodes.Potions.NPotion", "Reload"),
        ("MegaCrit.Sts2.Core.Nodes.Rewards.NRewardButton", "_Ready"),
        ("MegaCrit.Sts2.Core.Nodes.Rewards.NRewardButton", "Reload"),
        ("MegaCrit.Sts2.Core.Nodes.Rewards.NRewardButton", "SetReward"),
        ("MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder", "OnFocus"),
        ("MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder", "OnUnfocus"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantSlot", "OnFocus"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantSlot", "OnUnfocus"),
        ("MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton", "OnFocus"),
        ("MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton", "OnUnfocus"),
        ("MegaCrit.Sts2.Core.Nodes.Relics.NRelicBasicHolder", "OnFocus"),
        ("MegaCrit.Sts2.Core.Nodes.Relics.NRelicBasicHolder", "OnUnfocus"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic.NTreasureRoomRelicHolder", "OnFocus"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic.NTreasureRoomRelicHolder", "OnUnfocus"),
        ("MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom", "_Ready"),
        ("MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom", "_Ready"),
        ("MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom", "_Ready"),
        ("MegaCrit.Sts2.Core.Nodes.Rooms.NTreasureRoom", "_Ready"),
        ("MegaCrit.Sts2.Core.Nodes.Rooms.NRestSiteRoom", "_Ready"),
        ("MegaCrit.Sts2.Core.Nodes.Rooms.NMapRoom", "_Ready"),
        ("MegaCrit.Sts2.Core.Nodes.NRun", "_Ready"),
        ("MegaCrit.Sts2.Core.Nodes.NGame", "_Ready"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer", "Fill"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.Settings.NSettingsScreen", "_Ready"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.NRunSubmenuStack", "GetSubmenuType"),

        // SlayTheSpireMapFog.Godot targets
        ("MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen", "SetMap"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen", "RecalculateTravelability"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen", "RefreshAllPointVisuals"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint", "UpdateIcon"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint", "RefreshMarkedIconVisibility"),
        ("MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint", "OnHighlightPointType")
    ];

    public static int Main(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("Usage: Sts2CompatChecker <path-to-sts2.dll> [optional-mod-dlls...]");
            return 1;
        }

        string sts2Path = args[0];
        if (!File.Exists(sts2Path))
        {
            Console.Error.WriteLine($"[ERROR] sts2.dll not found at: {sts2Path}");
            return 1;
        }

        Console.WriteLine($"[INFO] Inspecting sts2.dll: {sts2Path}");

        using var sts2Stream = File.OpenRead(sts2Path);
        using var sts2PeReader = new PEReader(sts2Stream);
        var sts2Reader = sts2PeReader.GetMetadataReader();

        var types = new HashSet<string>(StringComparer.Ordinal);
        var baseTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        var members = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in sts2Reader.TypeDefinitions)
        {
            var typeDef = sts2Reader.GetTypeDefinition(handle);
            string fullName = GetFullTypeName(sts2Reader, handle);
            types.Add(fullName);

            if (!typeDef.BaseType.IsNil)
            {
                string? baseFullName = ResolveTypeNameFromHandle(sts2Reader, typeDef.BaseType);
                if (!string.IsNullOrEmpty(baseFullName))
                {
                    baseTypes[fullName] = baseFullName;
                }
            }

            foreach (var mHandle in typeDef.GetMethods())
            {
                var method = sts2Reader.GetMethodDefinition(mHandle);
                string mName = sts2Reader.GetString(method.Name);
                members.Add($"{fullName}.{mName}");
            }

            foreach (var pHandle in typeDef.GetProperties())
            {
                var prop = sts2Reader.GetPropertyDefinition(pHandle);
                string pName = sts2Reader.GetString(prop.Name);
                members.Add($"{fullName}.{pName}");
            }

            foreach (var fHandle in typeDef.GetFields())
            {
                var field = sts2Reader.GetFieldDefinition(fHandle);
                string fName = sts2Reader.GetString(field.Name);
                members.Add($"{fullName}.{fName}");
            }

            foreach (var eHandle in typeDef.GetEvents())
            {
                var ev = sts2Reader.GetEventDefinition(eHandle);
                string eName = sts2Reader.GetString(ev.Name);
                members.Add($"{fullName}.{eName}");
            }
        }

        Console.WriteLine($"[INFO] Parsed {types.Count} types and {members.Count} members from sts2.dll.");

        var missing = new List<string>();

        // 1. Verify dynamic string-based Harmony patch targets
        foreach (var (typeName, memberName) in StringPatchTargets)
        {
            if (!types.Contains(typeName))
            {
                missing.Add($"[String Patch] Missing Type: {typeName}");
                continue;
            }

            if (!HasMember(types, baseTypes, members, typeName, memberName))
            {
                missing.Add($"[String Patch] Missing Member: {typeName}.{memberName}");
            }
        }

        // 2. Verify compile-time references from provided mod DLLs
        var modDllPaths = new List<string>();
        for (int i = 1; i < args.Length; i++)
        {
            if (File.Exists(args[i]))
            {
                modDllPaths.Add(args[i]);
            }
        }

        int totalCompileRefsChecked = 0;
        foreach (string modDllPath in modDllPaths)
        {
            Console.WriteLine($"[INFO] Inspecting compile-time references in: {modDllPath}");
            using var modStream = File.OpenRead(modDllPath);
            using var modPeReader = new PEReader(modStream);
            var modReader = modPeReader.GetMetadataReader();

            // Check TypeReferences pointing to sts2
            foreach (var handle in modReader.TypeReferences)
            {
                var (asmName, typeName) = ResolveTypeReference(modReader, handle);
                if (string.Equals(asmName, "sts2", StringComparison.OrdinalIgnoreCase))
                {
                    totalCompileRefsChecked++;
                    if (!types.Contains(typeName))
                    {
                        missing.Add($"[{Path.GetFileName(modDllPath)}] Missing Compile-Time Type: {typeName}");
                    }
                }
            }

            // Check MemberReferences pointing to sts2
            foreach (var handle in modReader.MemberReferences)
            {
                var memberRef = modReader.GetMemberReference(handle);
                string memberName = modReader.GetString(memberRef.Name);

                var parent = memberRef.Parent;
                if (parent.Kind == HandleKind.TypeReference)
                {
                    var (asmName, typeName) = ResolveTypeReference(modReader, (TypeReferenceHandle)parent);
                    if (string.Equals(asmName, "sts2", StringComparison.OrdinalIgnoreCase))
                    {
                        totalCompileRefsChecked++;
                        if (!HasMember(types, baseTypes, members, typeName, memberName))
                        {
                            missing.Add($"[{Path.GetFileName(modDllPath)}] Missing Compile-Time Member: {typeName}.{memberName}");
                        }
                    }
                }
                else if (parent.Kind == HandleKind.TypeSpecification)
                {
                    var typeSpec = modReader.GetTypeSpecification((TypeSpecificationHandle)parent);
                    // Decode generic type specification if parent is a generic instantiation
                    var signature = typeSpec.DecodeSignature(new SimpleTypeProvider(), null);
                    if (signature != null && string.Equals(signature.AssemblyName, "sts2", StringComparison.OrdinalIgnoreCase))
                    {
                        totalCompileRefsChecked++;
                        if (!HasMember(types, baseTypes, members, signature.TypeName, memberName))
                        {
                            missing.Add($"[{Path.GetFileName(modDllPath)}] Missing Generic Member: {signature.TypeName}.{memberName}");
                        }
                    }
                }
            }
        }

        if (modDllPaths.Count > 0)
        {
            Console.WriteLine($"[INFO] Checked {totalCompileRefsChecked} compile-time references across {modDllPaths.Count} mod DLL(s).");
        }

        if (missing.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FAIL] Found {missing.Count} compatibility failures against sts2.dll:");
            foreach (var err in missing)
            {
                Console.WriteLine($"  - {err}");
            }
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[PASS] Compatibility check passed! All {StringPatchTargets.Length} string targets and {totalCompileRefsChecked} compile-time references verified successfully.");
        Console.ResetColor();
        return 0;
    }

    private static bool HasMember(HashSet<string> types, Dictionary<string, string> baseTypes, HashSet<string> members, string typeName, string memberName)
    {
        string? current = typeName;
        while (!string.IsNullOrEmpty(current) && types.Contains(current))
        {
            if (members.Contains($"{current}.{memberName}")) return true;
            if (!baseTypes.TryGetValue(current, out current)) break;
        }
        return false;
    }

    private static string GetFullTypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        string name = reader.GetString(typeDef.Name);
        if (typeDef.IsNested)
        {
            var declaringHandle = typeDef.GetDeclaringType();
            return $"{GetFullTypeName(reader, declaringHandle)}+{name}";
        }
        string ns = reader.GetString(typeDef.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string? ResolveTypeNameFromHandle(MetadataReader reader, EntityHandle handle)
    {
        if (handle.Kind == HandleKind.TypeDefinition)
        {
            return GetFullTypeName(reader, (TypeDefinitionHandle)handle);
        }
        if (handle.Kind == HandleKind.TypeReference)
        {
            var (_, fullName) = ResolveTypeReference(reader, (TypeReferenceHandle)handle);
            return fullName;
        }
        return null;
    }

    private static (string AssemblyName, string FullName) ResolveTypeReference(MetadataReader reader, TypeReferenceHandle handle)
    {
        var typeRef = reader.GetTypeReference(handle);
        string name = reader.GetString(typeRef.Name);
        var scope = typeRef.ResolutionScope;
        if (scope.Kind == HandleKind.AssemblyReference)
        {
            var asmRef = reader.GetAssemblyReference((AssemblyReferenceHandle)scope);
            string asmName = reader.GetString(asmRef.Name);
            string ns = reader.GetString(typeRef.Namespace);
            string fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            return (asmName, fullName);
        }
        else if (scope.Kind == HandleKind.TypeReference)
        {
            var (asmName, parentFullName) = ResolveTypeReference(reader, (TypeReferenceHandle)scope);
            return (asmName, $"{parentFullName}+{name}");
        }
        return (string.Empty, name);
    }

    private sealed class SimpleTypeInfo
    {
        public string AssemblyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
    }

    private sealed class SimpleTypeProvider : ISignatureTypeProvider<SimpleTypeInfo, object?>
    {
        public SimpleTypeInfo GetGenericInstantiation(SimpleTypeInfo genericType, System.Collections.Immutable.ImmutableArray<SimpleTypeInfo> typeArguments) => genericType;
        public SimpleTypeInfo GetArrayType(SimpleTypeInfo elementType, ArrayShape shape) => elementType;
        public SimpleTypeInfo GetByReferenceType(SimpleTypeInfo elementType) => elementType;
        public SimpleTypeInfo GetPointerType(SimpleTypeInfo elementType) => elementType;
        public SimpleTypeInfo GetFunctionPointerType(MethodSignature<SimpleTypeInfo> signature) => new();
        public SimpleTypeInfo GetGenericMethodParameter(object? genericContext, int index) => new();
        public SimpleTypeInfo GetGenericTypeParameter(object? genericContext, int index) => new();
        public SimpleTypeInfo GetModifiedType(SimpleTypeInfo modifier, SimpleTypeInfo unmodifiedType, bool isRequired) => unmodifiedType;
        public SimpleTypeInfo GetPinnedType(SimpleTypeInfo elementType) => elementType;
        public SimpleTypeInfo GetPrimitiveType(PrimitiveTypeCode typeCode) => new();
        public SimpleTypeInfo GetSZArrayType(SimpleTypeInfo elementType) => elementType;
        public SimpleTypeInfo GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            return new SimpleTypeInfo { AssemblyName = "sts2", TypeName = GetFullTypeName(reader, handle) };
        }
        public SimpleTypeInfo GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var (asm, full) = ResolveTypeReference(reader, handle);
            return new SimpleTypeInfo { AssemblyName = asm, TypeName = full };
        }
        public SimpleTypeInfo GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            var spec = reader.GetTypeSpecification(handle);
            return spec.DecodeSignature(this, genericContext);
        }
    }
}
