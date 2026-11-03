using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class GroupedInputScoreAdapterTests
    {
        private static void LoadTwinkle(out PlayPlan play, out List<InputUnit> units)
        {
            string path = Path.Combine(Application.dataPath, "Data", "Songs", "twinkle_easy.json");
            SongChart chart = JsonUtility.FromJson<SongChart>(File.ReadAllText(path));

            Assert.IsTrue(PlanBuilder.TryBuild(chart.Sections, chart.RepeatMappings,
                out play, out _, out string error), error);
            Assert.IsTrue(InputUnitBuilder.TryBuild(chart, play,
                out units, out error), error);
        }

        private static ScorePolicy DefaultPolicy()
        {
            return new ScorePolicy(100, 80, 60, 30, 0, 100, 0);
        }

        [Test]
        public void Twinkle_AllPerfect_TwelveJudgments_OneMillionPoints()
        {
            LoadTwinkle(out var play, out var units);
            var trials = units.Select(u => new TimedInputTrialResult
            {
                UnitId = u.UnitId,
                PitchJudgment = "Perfect",
                CapturedPosition = u.TargetPitchStep / 9f,
                HoldSucceeded = u.IsHold ? (bool?)true : null,
                TimedOut = false
            }).ToList();

            ScoreResult score = GroupedInputScoreAdapter.Calculate(
                play, units, trials, DefaultPolicy(), Difficulty.Easy, 0, 9);
            ScoreResult vocal = GroupedInputScoreAdapter.ExpandForVocalPlayback(score, units);

            Assert.AreEqual(1000000, score.Score);
            Assert.AreEqual(1000000, score.MaxScore);
            Assert.AreEqual(12, score.PerfectCount);
            Assert.AreEqual(12, score.UnitResults.Count);
            Assert.AreEqual(21, vocal.UnitResults.Count);
            Assert.AreEqual(21, vocal.UnitResults.Count(r => r.CapturedPitchStep.HasValue));
        }

        [Test]
        public void Twinkle_AllMissing_NoScore_NoSingingPitch()
        {
            LoadTwinkle(out var play, out var units);
            var trials = units.Select(u => new TimedInputTrialResult
            {
                UnitId = u.UnitId,
                PitchJudgment = "Miss",
                CapturedPosition = null,
                HoldSucceeded = u.IsHold ? (bool?)false : null,
                TimedOut = true
            }).ToList();

            ScoreResult score = GroupedInputScoreAdapter.Calculate(
                play, units, trials, DefaultPolicy(), Difficulty.Easy, 0, 9);
            ScoreResult vocal = GroupedInputScoreAdapter.ExpandForVocalPlayback(score, units);

            Assert.AreEqual(0, score.Score);
            Assert.IsTrue(score.TimedOut);
            Assert.AreEqual(12, score.UnitResults.Count);
            Assert.AreEqual(21, vocal.UnitResults.Count);
            Assert.IsTrue(vocal.UnitResults.All(r => !r.CapturedPitchStep.HasValue));
        }

        [Test]
        public void GroupedHold_UsesOneResultForEverySourceNote()
        {
            LoadTwinkle(out var play, out var units);
            var trials = units.Select(u => new TimedInputTrialResult
            {
                UnitId = u.UnitId,
                PitchJudgment = "Great",
                CapturedPosition = 0.3333f,
                HoldSucceeded = u.IsHold ? (bool?)false : null,
                TimedOut = false
            }).ToList();

            ScoreResult score = GroupedInputScoreAdapter.Calculate(
                play, units, trials, DefaultPolicy(), Difficulty.Easy, 0, 9);
            ScoreResult vocal = GroupedInputScoreAdapter.ExpandForVocalPlayback(score, units);

            var firstSources = units[0].SourceNoteIds;
            Assert.AreEqual(2, firstSources.Count);
            foreach (string id in firstSources)
            {
                var noteResult = vocal.UnitResults.Single(r => r.UnitId == id);
                Assert.AreEqual(InputState.Completed, noteResult.InputState);
                Assert.AreEqual(3, noteResult.CapturedPitchStep);
            }
            Assert.AreEqual(12, score.GreatCount);
            Assert.Less(score.Score, score.MaxScore);
        }
    }
}
