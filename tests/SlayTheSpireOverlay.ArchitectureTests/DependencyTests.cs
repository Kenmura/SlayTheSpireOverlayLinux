using System.Linq;
using System.Reflection;
using Godot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;
using SlayTheSpireMapFog.Core.Services;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Godot;

namespace SlayTheSpireOverlay.ArchitectureTests;

[TestClass]
public class DependencyTests
{
    private static readonly Assembly[] ModAssemblies =
    [
        typeof(ModEntry).Assembly,
        typeof(SlayTheSpireMapFog.Godot.ModEntry).Assembly
    ];

    [TestMethod]
    public void Core_Should_Not_Depend_On_Godot_Harmony_Or_Sts2()
    {
        foreach (var (asm, coreNamespace) in new[]
        {
            (typeof(ITierListProvider).Assembly, "SlayTheSpireOverlay.Core"),
            (typeof(MapFogCalculator).Assembly, "SlayTheSpireMapFog.Core")
        })
        {
            var result = Types.InAssembly(asm)
                .That()
                .ResideInNamespace(coreNamespace)
                .ShouldNot()
                .HaveDependencyOnAny(
                    "Godot",
                    "GodotSharp",
                    "MegaCrit",
                    "HarmonyLib",
                    "SlayTheSpireOverlay.Godot",
                    "SlayTheSpireMapFog.Godot")
                .GetResult();

            Assert.IsTrue(
                result.IsSuccessful,
                $"{coreNamespace} violated domain isolation: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [TestMethod]
    public void GodotUI_Should_Not_Depend_On_Network_IO_Harmony_Sts2_Or_ModEntry()
    {
        foreach (var (asm, uiNamespace) in new[]
        {
            (typeof(ModEntry).Assembly, "SlayTheSpireOverlay.Godot.UI"),
            (typeof(SlayTheSpireMapFog.Godot.ModEntry).Assembly, "SlayTheSpireMapFog.Godot.UI")
        })
        {
            var result = Types.InAssembly(asm)
                .That()
                .ResideInNamespace(uiNamespace)
                .ShouldNot()
                .HaveDependencyOnAny(
                    "System.Net",
                    "System.Net.Http",
                    "System.IO",
                    "HarmonyLib",
                    "MegaCrit",
                    "SlayTheSpireOverlay.Godot.ModEntry",
                    "SlayTheSpireOverlay.Godot.Sts2RuntimeInspector",
                    "SlayTheSpireMapFog.Godot.ModEntry",
                    "SlayTheSpireMapFog.Godot.Sts2MapInspector")
                .GetResult();

            Assert.IsTrue(
                result.IsSuccessful,
                $"{uiNamespace} violated layer isolation: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [TestMethod]
    public void ModAssembly_Should_Never_Subclass_GodotObject()
    {
        var subclasses = ModAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsSubclassOf(typeof(GodotObject)))
            .Select(t => t.FullName)
            .ToList();

        Assert.IsEmpty(
            subclasses,
            $"AGENTS.md §4.1 violation: Mod types must never subclass GodotObject/Node/Control ({string.Join(", ", subclasses)})");
    }

    [TestMethod]
    public void AllTypes_Should_Not_Declare_Public_Mutable_Fields()
    {
        var offendingFields = ModAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => !t.IsEnum)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(f => !f.IsLiteral && !f.IsInitOnly)
            .Select(f => $"{f.DeclaringType?.FullName}.{f.Name}")
            .ToList();

        Assert.IsEmpty(
            offendingFields,
            $"Public mutable fields are forbidden: {string.Join(", ", offendingFields)}");
    }
}
