using SlayTheSpireMod.Common;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class ReflectionAccessTests
{
    private class Base
    {
        private readonly string _baseSecret = "base";

        public string BaseSecretForTest => _baseSecret;
    }

    private sealed class Sample : Base
    {
#pragma warning disable CA1822
        private readonly int _hidden = 7;
        public static string StaticName => "static-value";
        public int HiddenForTest => _hidden;
        public string? NullProp => null;
        public string Named => "prop";
#pragma warning restore CA1822
    }

    [TestMethod]
    public void GetMember_ReadsPublicPropertyAndPrivateField()
    {
        var sample = new Sample();
        Assert.AreEqual("prop", ReflectionAccess.GetMember(sample, "Named"));
        Assert.AreEqual(7, ReflectionAccess.GetMember(sample, "_hidden"));
    }

    [TestMethod]
    public void GetMember_FindsMembersDeclaredOnBaseTypes()
    {
        Assert.AreEqual("base", ReflectionAccess.GetMember(new Sample(), "_baseSecret"));
    }

    [TestMethod]
    public void GetMember_SkipsNullAndMissingNamesAndUsesTheFirstNonNull()
    {
        var sample = new Sample();
        Assert.AreEqual("prop", ReflectionAccess.GetMember(sample, "Missing", "NullProp", "Named"));
        Assert.IsNull(ReflectionAccess.GetMember(sample, "Missing"));
    }

    [TestMethod]
    public void GetMember_NullTargetReturnsNull()
    {
        Assert.IsNull(ReflectionAccess.GetMember(null, "Anything"));
    }

    [TestMethod]
    public void GetStaticMember_ReadsStaticOnlyAndIgnoresInstanceMembers()
    {
        Assert.AreEqual("static-value", ReflectionAccess.GetStaticMember(typeof(Sample), "StaticName"));
        Assert.IsNull(ReflectionAccess.GetStaticMember(typeof(Sample), "Named"));
    }

    [TestMethod]
    public void GetCachedType_ResolvesLoadedTypesAndReturnsNullForUnknown()
    {
        Assert.AreEqual(typeof(string), ReflectionAccess.GetCachedType("System.String"));
        Assert.IsNull(ReflectionAccess.GetCachedType("No.Such.Type"));
    }

    [TestMethod]
    public void GetCachedMethod_FindsInheritedPrivateAndMissingMethods()
    {
        Assert.IsNotNull(ReflectionAccess.GetCachedMethod(typeof(Sample), "get_BaseSecretForTest"));
        Assert.IsNull(ReflectionAccess.GetCachedMethod(typeof(Sample), "NoSuchMethod"));
    }
}
