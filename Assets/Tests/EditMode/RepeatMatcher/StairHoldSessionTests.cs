using NUnit.Framework;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class StairHoldSessionTests
    {
        [Test]
        public void CapturesEachRedPressInOrderOnlyWhileHeld()
        {
            var session = new StairHoldSession(3);
            Assert.IsFalse(session.TryCapture(0.1f));
            session.Begin();
            Assert.IsTrue(session.TryCapture(0.8f));
            Assert.IsTrue(session.TryCapture(0.5f));
            Assert.IsTrue(session.TryCapture(0.2f));
            Assert.IsTrue(session.IsComplete);
            Assert.IsFalse(session.TryCapture(0.1f));
            Assert.AreEqual(0.8f, session.GetPosition(0));
            Assert.AreEqual(0.5f, session.GetPosition(1));
            Assert.AreEqual(0.2f, session.GetPosition(2));
        }

        [Test]
        public void ReleaseAndInvalidPositionsDoNotAddCaptures()
        {
            var session = new StairHoldSession(2);
            session.Begin();
            Assert.IsFalse(session.TryCapture(float.NaN));
            Assert.IsTrue(session.TryCapture(0.5f));
            session.End();
            Assert.IsFalse(session.TryCapture(0.7f));
            Assert.AreEqual(1, session.CapturedCount);
            Assert.IsFalse(session.IsComplete);
        }
    }
}
