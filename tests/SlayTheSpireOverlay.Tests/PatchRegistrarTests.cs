using SlayTheSpireMod.Common;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class PatchRegistrarTests
{
    [TestMethod]
    public void TryApply_OneFailureDoesNotStopOthersAndIsReported()
    {
        var log = new List<string>();
        var registrar = new PatchRegistrar("[T]", log.Add);
        int ran = 0;

        Assert.IsTrue(registrar.TryApply("a", () => ran++));
        Assert.IsFalse(registrar.TryApply("b", () => throw new MissingMemberException("NCard.Nope")));
        Assert.IsTrue(registrar.TryApply("c", () => ran++));

        Assert.AreEqual(2, ran);
        Assert.AreEqual(2, registrar.AppliedCount);
        Assert.AreEqual(1, registrar.SkippedCount);
        Assert.AreEqual("b", registrar.Skipped[0].Name);
        Assert.AreEqual("[T] patch 'b' skipped: NCard.Nope", log[0]);
        Assert.AreEqual("[T] patches: applied 2, skipped 1", registrar.Summary());
    }
}
