using System.Collections.Generic;
using NUnit.Framework;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class NoteLevelRepeatTests
    {
        private static Section Example() => new Section
        {
            SongId = "TEST", SectionId = "A", StartMeasure = 1, EndMeasure = 1,
            Notes = new List<Note>
            {
                new Note { NoteId = "A", MeasureNumber = 1, NoteIndexInMeasure = 0, TargetPitchStep = 2, DurationBeats = 1 },
                new Note { NoteId = "B", MeasureNumber = 1, NoteIndexInMeasure = 1, TargetPitchStep = 4, DurationBeats = 1 },
                new Note { NoteId = "C", MeasureNumber = 1, NoteIndexInMeasure = 2, TargetPitchStep = 2, DurationBeats = 0.5f, RepeatSourceNoteId = "A" },
                new Note { NoteId = "D", MeasureNumber = 1, NoteIndexInMeasure = 3, TargetPitchStep = 4, DurationBeats = 1.5f, RepeatSourceNoteId = "B" }
            }
        };

        [Test]
        public void RepeatsWithinMeasureReuseInputAndRetainPlaybackNotes()
        {
            var section = Example();
            Assert.IsTrue(PlanBuilder.TryBuild(new[] { section }, new RepeatMapping[0],
                out var play, out var playback, out var error), error);
            Assert.AreEqual(2, play.Notes.Count);
            CollectionAssert.AreEqual(new[] { "A", "B", "A", "B" }, playback.Sections[0].SourceNoteIds);
            var chart = new SongChart { SongId = "TEST", Bpm = 120, Sections = new List<Section> { section } };
            Assert.IsTrue(InputUnitBuilder.TryBuild(chart, play, out var units, out error), error);
            Assert.AreEqual(2, units.Count);
        }

        [Test]
        public void InvalidRepeatPitchAndIndirectSourceAreRejected()
        {
            var section = Example();
            section.Notes[2].TargetPitchStep = 7;
            Assert.IsFalse(PlanBuilder.TryBuild(new[] { section }, new RepeatMapping[0], out _, out _, out _));
            section = Example();
            section.Notes[3].RepeatSourceNoteId = "C";
            Assert.IsFalse(PlanBuilder.TryBuild(new[] { section }, new RepeatMapping[0], out _, out _, out _));
        }
    }
}
