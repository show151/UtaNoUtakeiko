using System.Collections.Generic;
using NUnit.Framework;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class StairGameplayTests
    {
        private static List<InputUnit> Build(params Note[] notes)
        {
            var chart = new SongChart { SongId = "TEST", Bpm = 120 };
            chart.Sections.Add(new Section { SongId = "TEST", SectionId = "A", StartMeasure = 1, EndMeasure = 1, Notes = new List<Note>(notes) });
            var plan = new PlayPlan { SongId = "TEST", SectionIds = new List<string> { "A" }, Notes = new List<Note>(notes) };
            Assert.IsTrue(InputUnitBuilder.TryBuild(chart, plan, out var units, out var error), error);
            return units;
        }

        [Test]
        public void ExplicitStairNotesRemainIndividuallyScorableEvenWhenPitchesMatch()
        {
            var units = Build(
                new Note { NoteId = "A", MeasureNumber = 1, NoteIndexInMeasure = 0, DurationBeats = 1, TargetPitchStep = 4, StairGroupId = "STAIR" },
                new Note { NoteId = "B", MeasureNumber = 1, NoteIndexInMeasure = 1, DurationBeats = 1, TargetPitchStep = 4, StairGroupId = "STAIR" },
                new Note { NoteId = "C", MeasureNumber = 1, NoteIndexInMeasure = 2, DurationBeats = 1, TargetPitchStep = 2, StairGroupId = "STAIR" });
            Assert.AreEqual(3, units.Count);
            Assert.IsFalse(units[0].IsHold);
            Assert.AreEqual("STAIR", units[2].StairGroupId);

            var trial = new TimedInputTrial(units);
            var resolved = new List<TimedInputTrialResult>();
            trial.OnResolved += resolved.Add;
            Assert.IsTrue(trial.TryRecordPlayerRelease(0, "Perfect", 0.4f, 0, 0));
            Assert.IsTrue(trial.TryRecordPlayerRelease(1, "Great", 0.3f, 0, 0));
            Assert.IsTrue(trial.TryRecordMissingInput(2));
            Assert.IsFalse(trial.TryRecordMissingInput(2));
            Assert.AreEqual(3, trial.ResolvedCount);
            Assert.IsTrue(resolved[2].TimedOut);
            Assert.IsNull(resolved[2].CapturedPosition);
        }

        [Test]
        public void StairCapturesKeepEachPitchForScoringAndVocalPlayback()
        {
            var notes = new[]
            {
                new Note { NoteId = "A", MeasureNumber = 1, NoteIndexInMeasure = 0, DurationBeats = 1, TargetPitchStep = 4, StairGroupId = "STAIR" },
                new Note { NoteId = "B", MeasureNumber = 1, NoteIndexInMeasure = 1, DurationBeats = 1, TargetPitchStep = 3, StairGroupId = "STAIR" }
            };
            var units = Build(notes);
            var plan = new PlayPlan { SongId = "TEST", Notes = new List<Note>(notes) };
            var trials = new List<TimedInputTrialResult>
            {
                new TimedInputTrialResult { UnitId = "A", PitchJudgment = "Perfect", CapturedPosition = 0.4f },
                new TimedInputTrialResult { UnitId = "B", PitchJudgment = "Great", CapturedPosition = 0.3f }
            };
            var score = GroupedInputScoreAdapter.Calculate(plan, units, trials,
                new ScorePolicy(100, 80, 50, 20, 0, 100, 0), Difficulty.Easy, 0, 10);
            var vocal = GroupedInputScoreAdapter.ExpandForVocalPlayback(score, units);
            Assert.AreEqual(1, score.PerfectCount);
            Assert.AreEqual(1, score.GreatCount);
            Assert.AreEqual(4, vocal.UnitResults[0].CapturedPitchStep);
            Assert.AreEqual(3, vocal.UnitResults[1].CapturedPitchStep);
        }
    }
}
