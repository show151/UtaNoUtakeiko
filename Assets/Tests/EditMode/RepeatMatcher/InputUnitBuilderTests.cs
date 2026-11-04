using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class InputUnitBuilderTests
    {
        [Test]
        public void TwinkleEasy_Groups21NotesInto12Units_AndScalesTimeline()
        {
            string path = Path.Combine(Application.dataPath, "Data", "Songs", "twinkle_easy.json");
            var chart = JsonUtility.FromJson<SongChart>(File.ReadAllText(path));

            Assert.IsTrue(PlanBuilder.TryBuild(chart.Sections, chart.RepeatMappings,
                out PlayPlan playPlan, out _, out string planError), planError);

            Assert.IsTrue(InputUnitBuilder.TryBuild(chart, playPlan,
                out List<InputUnit> units, out string error), error);

            Assert.AreEqual(21, playPlan.Notes.Count);
            Assert.AreEqual(12, units.Count);
            Assert.AreEqual(9, units.Count(u => u.IsHold));
            Assert.AreEqual(3, units.Count(u => !u.IsHold));
            Assert.AreEqual(21, units.Sum(u => u.SourceNoteIds.Count));
            Assert.AreEqual(10.08f, units[units.Count - 1].EndSeconds, 0.001f);
            Assert.AreEqual(0f, units[0].StartSeconds, 0.001f);
            Assert.AreEqual(0.84f, units[0].DurationSeconds, 0.001f);
            CollectionAssert.AreEqual(
                new[] { "TWINKLE_M01_N01", "TWINKLE_M01_N02" },
                units[0].SourceNoteIds);
        }

        [Test]
        public void LongSingleNoteIsNotHold_AndRestSplitsIdenticalPitch()
        {
            var chart = new SongChart { SongId = "TEST", Bpm = 120 };
            chart.Sections.Add(new Section
            {
                SongId = "TEST", SectionId = "A", StartMeasure = 1, EndMeasure = 1,
                Notes = new List<Note>
                {
                    new Note { NoteId = "A", MeasureNumber = 1, TargetPitchStep = 0, DurationBeats = 2 },
                    new Note { NoteId = "R", MeasureNumber = 1, IsRest = true, DurationBeats = 1 },
                    new Note { NoteId = "B", MeasureNumber = 1, TargetPitchStep = 0, DurationBeats = 1 }
                }
            });
            var plan = new PlayPlan
            {
                SongId = "TEST", SectionIds = new List<string> { "A" },
                Notes = new List<Note> { chart.Sections[0].Notes[0], chart.Sections[0].Notes[2] }
            };

            Assert.IsTrue(InputUnitBuilder.TryBuild(chart, plan,
                out List<InputUnit> units, out string error), error);
            Assert.AreEqual(2, units.Count);
            Assert.IsFalse(units[0].IsHold);
            Assert.IsFalse(units[1].IsHold);
            Assert.AreEqual(0.7f, units[0].DurationSeconds, 0.001f);
            Assert.AreEqual(1.05f, units[1].StartSeconds, 0.001f);
        }

        [Test]
        public void EqualPitchAcrossMeasureBoundaryIsOneHold()
        {
            var chart = new SongChart { SongId = "TEST", Bpm = 100 };
            chart.Sections.Add(new Section
            {
                SongId = "TEST", SectionId = "A", StartMeasure = 1, EndMeasure = 2,
                Notes = new List<Note>
                {
                    new Note { NoteId = "A", MeasureNumber = 1, TargetPitchStep = 3, DurationBeats = 1 },
                    new Note { NoteId = "B", MeasureNumber = 2, TargetPitchStep = 3, DurationBeats = 1 }
                }
            });
            var plan = new PlayPlan
            {
                SongId = "TEST", SectionIds = new List<string> { "A" },
                Notes = new List<Note>(chart.Sections[0].Notes)
            };

            Assert.IsTrue(InputUnitBuilder.TryBuild(chart, plan,
                out List<InputUnit> units, out string error), error);
            Assert.AreEqual(1, units.Count);
            Assert.IsTrue(units[0].IsHold);
            Assert.AreEqual(2, units[0].SourceNoteIds.Count);
            Assert.AreEqual(2f, units[0].RequiredHoldSeconds, 0.001f);
        }
    }
}
