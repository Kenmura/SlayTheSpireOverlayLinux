using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Godot;

namespace SlayTheSpireOverlay.ArchitectureTests;

[TestClass]
public class DependencyTests
{
    [TestMethod]
    public void Core_Should_Not_Reference_Godot_Assemblies()
    {
        var result = Types.InAssembly(typeof(ITierListProvider).Assembly)
            .That()
            .ResideInNamespace("SlayTheSpireOverlay.Core")
            .ShouldNot()
            .HaveDependencyOn("GodotSharp")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful, "Core project violated domain isolation by referencing Godot Sharp!");
    }

    [TestMethod]
    public void GodotUI_Should_Not_Directly_Instantiate_HttpClient()
    {
        var result = Types.InAssembly(typeof(ModEntry).Assembly)
            .That()
            .ResideInNamespace("SlayTheSpireOverlay.Godot.UI")
            .ShouldNot()
            .HaveDependencyOn("System.Net.Http")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful, "UI controls must depend on DI-provided ITierListProvider rather than direct networking.");
    }
}
