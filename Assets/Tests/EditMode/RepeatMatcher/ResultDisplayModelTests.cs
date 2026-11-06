using System.Collections.Generic;
using NUnit.Framework;
using Uta.GameFlow;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class ResultDisplayModelTests
    {
        [Test]
        public void CreatingDisplayAgain_DoesNotChangeConfirmedScoreResult()
        {
            var chart = new SongChart
            {
                SongId = "TWINKLE_TWINKLE",
                DisplayName = "きらきら星"
            };
            var unitResults = new List<UnitResult>();
            var result = new ScoreResult
            {
                SongId = chart.SongId,
                Score = 780000,
                MaxScore = 1000000,
                PerfectCount = 8,
                GreatCount = 2,
                GoodCount = 1,
                BadCount = 1,
                MissCount = 0,
                UnitResults = unitResults
            };

            ResultDisplayModel first = ResultDisplayModel.Create(chart, result);
            ResultDisplayModel second = ResultDisplayModel.Create(chart, result);

            Assert.AreEqual("きらきら星", first.SongName);
            Assert.AreEqual(first.SongName, second.SongName);
            Assert.AreEqual(first.Score, second.Score);
            Assert.AreEqual(first.AchievementRate, second.AchievementRate);
            Assert.AreEqual(first.Rank, second.Rank);
            Assert.AreEqual(first.JudgmentSummary, second.JudgmentSummary);
            Assert.AreEqual(first.Message, second.Message);

            Assert.AreEqual(780000, result.Score);
            Assert.AreEqual(1000000, result.MaxScore);
            Assert.AreEqual(8, result.PerfectCount);
            Assert.AreEqual(2, result.GreatCount);
            Assert.AreEqual(1, result.GoodCount);
            Assert.AreEqual(1, result.BadCount);
            Assert.AreEqual(0, result.MissCount);
            Assert.AreSame(unitResults, result.UnitResults);
        }
    }
}
