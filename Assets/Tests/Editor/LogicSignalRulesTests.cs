using NUnit.Framework;

public class LogicSignalRulesTests
{
    [Test]
    public void NoSourcesResolveLow()
    {
        Assert.AreEqual(0, LogicSignalRules.ResolveSources(new int[0]));
    }

    [Test]
    public void HighSourceDrivesEntireWireNetworkHigh()
    {
        Assert.AreEqual(1, LogicSignalRules.ResolveSources(new[] { 0, 1, 0 }));
    }

    [Test]
    public void MultipleLowSourcesKeepJunctionLow()
    {
        Assert.AreEqual(0, LogicSignalRules.ResolveSources(new[] { 0, 0, 0 }));
    }
}