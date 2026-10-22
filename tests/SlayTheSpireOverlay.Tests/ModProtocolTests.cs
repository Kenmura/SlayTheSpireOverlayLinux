using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class ModProtocolTests
{
    [TestMethod]
    public void SameVersion_IsCompatible()
    {
        Assert.IsTrue(ModProtocol.IsCompatible(ModProtocol.Version));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(2)]
    public void DifferentVersion_IsNotCompatible(int remote)
    {
        Assert.AreNotEqual(ModProtocol.Version, remote);
        Assert.IsFalse(ModProtocol.IsCompatible(remote));
    }

    [TestMethod]
    public void Version_FitsTheEightBitsTheHelloUses()
    {
        Assert.IsGreaterThan(0, ModProtocol.Version);
        Assert.IsLessThan(256, ModProtocol.Version);
    }
}
