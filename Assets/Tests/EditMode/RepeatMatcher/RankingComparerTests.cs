using System;
using System.Collections.Generic;
using NUnit.Framework;
using UtaNoUtakeiko.Gameplay;
using UtaNoUtakeiko.Ranking;

namespace UtaNoUtakeiko.Tests
{
    public class RankingComparerTests
    {
        private static ScoreResult Result(int score = 100, int perfect = 3, int great = 2, int good = 1, int bad = 1, int miss = 1)
        {
            return new ScoreResult { SongId = "song", Difficulty = Difficulty.Easy, Score = score, MaxScore = 1000,
                PerfectCount = perfect, GreatCount = great, GoodCount = good, BadCount = bad, MissCount = miss };
        }

        [Test]
        public void Comparison_UsesSpecifiedOrderAndDirections()
        {
            var comparer = new RankingComparer();
            var baseline = Result();
            var better = new[] { Result(score: 101, perfect: 0), Result(perfect: 4, great: 0),
                Result(great: 3, good: 0), Result(good: 2, bad: 10), Result(bad: 0, miss: 10), Result(miss: 0) };
            foreach (var entry in better)
            {
                Assert.Less(comparer.Compare(entry, baseline), 0);
                Assert.Greater(comparer.Compare(baseline, entry), 0);
            }
            Assert.AreEqual(0, comparer.Compare(baseline, Result()));
        }

        [Test]
        public void Calculate_UsesCompetitionRanks_AndFiltersSongAndDifficulty()
        {
            var a = Result(score: 200); var b = Result(score: 200); var c = Result(score: 100);
            var otherSong = Result(score: 300); otherSong.SongId = "other";
            var otherDifficulty = Result(score: 300); otherDifficulty.Difficulty = Difficulty.Hard;
            var ranked = RankingCalculator.Calculate(new[] { c, otherSong, a, otherDifficulty, b }, "song", Difficulty.Easy);
            Assert.AreEqual(3, ranked.Count);
            CollectionAssert.AreEqual(new[] { 1, 1, 3 }, ranked.ConvertAll(x => x.Rank));
            Assert.Throws<ArgumentException>(() => new RankingComparer().Compare(a, otherSong));
            Assert.Throws<ArgumentException>(() => new RankingComparer().Compare(a, otherDifficulty));
        }

        [Test]
        public void InvalidScores_AreRejected()
        {
            var invalid = Result(); invalid.Score = -1;
            Assert.Throws<ArgumentException>(() => RankingCalculator.Calculate(new[] { invalid }, "song", Difficulty.Easy));
            Assert.IsEmpty(RankingCalculator.Calculate(new ScoreResult[0], "song", Difficulty.Easy));
        }
    }
}

