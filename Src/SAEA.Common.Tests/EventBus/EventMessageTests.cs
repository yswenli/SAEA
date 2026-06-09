using System;
using SAEA.Common.EventBus;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class EventMessageTests
{
    [TestMethod]
    public void EventMessage_ShouldInitializeCorrectly()
    {
        var data = new byte[] { 1, 2, 3 };
        var context = new { Key = "value" };
        
        var msg = new EventMessage("test.topic", data, context);
        
        Assert.AreEqual("test.topic", msg.Topic);
        CollectionAssert.AreEqual(data, msg.Data);
        Assert.AreEqual(context, msg.Context);
        Assert.IsTrue(msg.Timestamp <= DateTime.UtcNow);
    }
    
    [TestMethod]
    public void EventMessage_ShouldAllowNullContext()
    {
        var msg = new EventMessage("test.topic", new byte[] { 1 });
        Assert.IsNull(msg.Context);
    }
}