using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class InputEdgeTests
{
    [TestMethod]
    public void EdgeDetector_FiresOncePerPress()
    {
        var edge = new EdgeDetector();
        Assert.IsFalse(edge.Update(false));
        Assert.IsTrue(edge.Update(true));
        Assert.IsFalse(edge.Update(true));
        Assert.IsFalse(edge.Update(false));
        Assert.IsTrue(edge.Update(true));
    }

    [TestMethod]
    public void FrameGate_BlocksRepeatsWithinAFrameButNotFrameZero()
    {
        var gate = new FrameGate();
        Assert.IsTrue(gate.ShouldProcess(10));
        Assert.IsFalse(gate.ShouldProcess(10));
        Assert.IsTrue(gate.ShouldProcess(11));
        Assert.IsTrue(gate.ShouldProcess(0));
        Assert.IsTrue(gate.ShouldProcess(0));
    }
}
