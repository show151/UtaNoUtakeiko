using NUnit.Framework;
using Uta.GameFlow;

namespace UtaNoUtakeiko.Tests
{
    public class GameplayProgressTests
    {
        [Test]
        public void ProgressCountsEachResolvedInputOnceAndResetsBetweenGames()
        {
            var progress = new GameplayProgressModel();
            Assert.AreEqual(0f, progress.Fraction(0));
            Assert.AreEqual(0f, progress.Fraction(4));
            progress.Record("first");
            progress.Record("first");
            progress.Record(null);
            Assert.AreEqual(0.25f, progress.Fraction(4));
            progress.Record("second");
            progress.Record("third");
            progress.Record("fourth");
            Assert.AreEqual(1f, progress.Fraction(4));
            progress.Reset();
            Assert.AreEqual(0f, progress.Fraction(4));
        }
    }
}
