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
        public void LongSingleNoteIgnoresLegacyHoldFlag_AndRestSplitsIdenticalPitch()
        {
            var chart = new SongChart { SongId = "TEST", Bpm = 120 };
            chart.Sections.Add(new Section
            {
                SongId = "TEST", SectionId = "A", StartMeasure = 1, EndMeasure = 1,
                Notes = new List<Note>
                {
                    // A tie-extended duration is one note. Legacy IsHold metadata
                    // must not determine the new gameplay input type.
                    new Note { NoteId = "A", MeasureNumber = 1, TargetPitchStep = 0, DurationBeats = 2, IsHold = true },
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
        public void TieLikeFragmentsMergeByPitch_AndTupletPitchChangesStayTap()
        {
            var chart = new SongChart { SongId = "TEST", Bpm = 120 };
            chart.Sections.Add(new Section
            {
                SongId = "TEST", SectionId = "A", StartMeasure = 1, EndMeasure = 1,
                Notes = new List<Note>
                {
                    // The model has no tie/tuplet symbol fields. These two
                    // adjacent equal-pitch notes represent tie-like fragments;
                    // the three 1/3-beat notes represent a triplet.
                    new Note { NoteId = "T1", MeasureNumber = 1, NoteIndexInMeasure = 0, TargetPitchStep = 0, DurationBeats = 0.5f },
                    new Note { NoteId = "T2", MeasureNumber = 1, NoteIndexInMeasure = 1, TargetPitchStep = 0, DurationBeats = 0.5f },
                    new Note { NoteId = "T3", MeasureNumber = 1, NoteIndexInMeasure = 2, TargetPitchStep = 1, DurationBeats = 1f / 3f },
                    new Note { NoteId = "T4", MeasureNumber = 1, NoteIndexInMeasure = 3, TargetPitchStep = 2, DurationBeats = 1f / 3f },
                    new Note { NoteId = "T5", MeasureNumber = 1, NoteIndexInMeasure = 4, TargetPitchStep = 3, DurationBeats = 1f / 3f },
                    new Note { NoteId = "REST", MeasureNumber = 1, NoteIndexInMeasure = 5, IsRest = true, DurationBeats = 2f }
                }
            });
            var plan = new PlayPlan
            {
                SongId = "TEST", SectionIds = new List<string> { "A" },
                Notes = new List<Note>(chart.Sections[0].Notes.Where(note => !note.IsRest))
            };

            Assert.IsTrue(InputUnitBuilder.TryBuild(chart, plan,
                out List<InputUnit> units, out string error), error);

            Assert.AreEqual(4, units.Count);
            Assert.IsTrue(units[0].IsHold);
            Assert.IsFalse(units[1].IsHold);
            Assert.IsFalse(units[2].IsHold);
            Assert.IsFalse(units[3].IsHold);
            CollectionAssert.AreEqual(new[] { "T1", "T2" }, units[0].SourceNoteIds);
            CollectionAssert.AreEqual(new[] { "T3" }, units[1].SourceNoteIds);
            CollectionAssert.AreEqual(new[] { "T4" }, units[2].SourceNoteIds);
            CollectionAssert.AreEqual(new[] { "T5" }, units[3].SourceNoteIds);
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
            Assert.AreEqual(1f, units[0].RequiredHoldSeconds, 0.001f);
        }

        [Test]
        public void ThreeEqualNotesRequireOnlyOneSecond()
        {
            var chart = new SongChart { SongId = "TEST", Bpm = 120 };
            var section = new Section
            {
                SongId = "TEST", SectionId = "A", StartMeasure = 1, EndMeasure = 1,
                Notes = new List<Note>
                {
                    new Note { NoteId = "A", MeasureNumber = 1, NoteIndexInMeasure = 0, TargetPitchStep = 2, DurationBeats = 1 },
                    new Note { NoteId = "B", MeasureNumber = 1, NoteIndexInMeasure = 1, TargetPitchStep = 2, DurationBeats = 1 },
                    new Note { NoteId = "C", MeasureNumber = 1, NoteIndexInMeasure = 2, TargetPitchStep = 2, DurationBeats = 1 }
                }
            };
            chart.Sections.Add(section);
            var plan = new PlayPlan { SongId = "TEST", SectionIds = new List<string> { "A" }, Notes = new List<Note>(section.Notes) };

            Assert.IsTrue(InputUnitBuilder.TryBuild(chart, plan, out List<InputUnit> units, out string error), error);
            Assert.AreEqual(1, units.Count);
            Assert.AreEqual(3, units[0].SourceNoteIds.Count);
            Assert.AreEqual(1f, units[0].RequiredHoldSeconds, 0.001f);
        }
    }
}

