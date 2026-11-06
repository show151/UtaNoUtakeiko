using NUnit.Framework;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

public class RepeatMatcherTests
{
    private Section CreateSection(
        string sectionId,
        int measure,
        params int[] pitches)
    {
        var section = new Section
        {
            SongId = "TEMP_SONG_01",
            SectionId = sectionId,
            StartMeasure = measure,
            EndMeasure = measure
        };

        for (int i = 0; i < pitches.Length; i++)
        {
            section.Notes.Add(new Note
            {
                NoteId = sectionId + "_NOTE_" + i,
                MeasureNumber = measure,
                NoteIndexInMeasure = i,
                TargetPitchStep = pitches[i],
                DurationBeats = 1f
            });
        }

        return section;
    }

    [Test]
    public void SamePitches_CreatesMapping()
    {
        var source = CreateSection("TEMP_A", 1, 0, 0, 7, 7);
        var target = CreateSection("TEMP_B", 9, 0, 0, 7, 7);

        bool matched = RepeatMatcher.TryMatch(
            source, target, out var mapping, out var error);

        Assert.IsTrue(matched, error);
        Assert.IsNotNull(mapping);
        Assert.AreEqual("TEMP_A", mapping.SourceSectionId);
        Assert.AreEqual("TEMP_B", mapping.TargetSectionId);
    }

    [Test]
    public void DifferentPitch_RejectsMapping()
    {
        var source = CreateSection("TEMP_A", 1, 0, 0, 7, 7);
        var target = CreateSection("TEMP_B", 9, 0, 0, 5, 7);

        bool matched = RepeatMatcher.TryMatch(
            source, target, out var mapping, out _);

        Assert.IsFalse(matched);
        Assert.IsNull(mapping);
    }

    [Test]
    public void DifferentNoteCount_RejectsMapping()
    {
        var source = CreateSection("TEMP_A", 1, 0, 0, 7, 7);
        var target = CreateSection("TEMP_B", 9, 0, 0, 7);

        bool matched = RepeatMatcher.TryMatch(
            source, target, out var mapping, out _);

        Assert.IsFalse(matched);
        Assert.IsNull(mapping);
    }

    [Test]
    public void DifferentDurations_StillMatches()
    {
        var source = CreateSection("TEMP_A", 1, 0, 7);
        var target = CreateSection("TEMP_B", 9, 0, 7);

        target.Notes[0].DurationBeats = 2f;

        bool matched = RepeatMatcher.TryMatch(
            source, target, out var mapping, out var error);

        Assert.IsTrue(matched, error);
        Assert.IsNotNull(mapping);
    }

    [Test]
    public void RestsAreExcludedFromPitchComparison()
    {
        var source = CreateSection("TEMP_A", 1, 0, 7);
        var target = CreateSection("TEMP_B", 9, 0, 7);

        target.Notes.Add(new Note
        {
            NoteId = "REST_01",
            MeasureNumber = 9,
            NoteIndexInMeasure = 2,
            DurationBeats = 1f,
            IsRest = true
        });

        bool matched = RepeatMatcher.TryMatch(
            source, target, out var mapping, out var error);

        Assert.IsTrue(matched, error);
        Assert.IsNotNull(mapping);
    }

    [Test]
    public void TieLikeFragmentsAndTupletDurations_DoNotChangeRepeatJudgment()
    {
        // The chart model has no separate tie/tuplet flags: tie-like values are
        // represented as adjacent same-pitch Notes, and tuplets as fractional
        // DurationBeats. Repeat matching still uses only playable pitch order.
        var source = new Section
        {
            SongId = "TEMP_SONG_01",
            SectionId = "TEMP_A",
            StartMeasure = 1,
            EndMeasure = 1,
            Notes = new System.Collections.Generic.List<Note>
            {
                new Note { NoteId = "A1", MeasureNumber = 1, NoteIndexInMeasure = 0, TargetPitchStep = 0, DurationBeats = 0.5f },
                new Note { NoteId = "A2", MeasureNumber = 1, NoteIndexInMeasure = 1, TargetPitchStep = 0, DurationBeats = 0.5f },
                new Note { NoteId = "AR1", MeasureNumber = 1, NoteIndexInMeasure = 2, IsRest = true, DurationBeats = 1f },
                new Note { NoteId = "A3", MeasureNumber = 1, NoteIndexInMeasure = 3, TargetPitchStep = 7, DurationBeats = 1f / 3f },
                new Note { NoteId = "A4", MeasureNumber = 1, NoteIndexInMeasure = 4, TargetPitchStep = 5, DurationBeats = 1f / 3f },
                new Note { NoteId = "A5", MeasureNumber = 1, NoteIndexInMeasure = 5, TargetPitchStep = 2, DurationBeats = 1f / 3f },
                new Note { NoteId = "AR2", MeasureNumber = 1, NoteIndexInMeasure = 6, IsRest = true, DurationBeats = 1f }
            }
        };
        var target = new Section
        {
            SongId = "TEMP_SONG_01",
            SectionId = "TEMP_B",
            StartMeasure = 9,
            EndMeasure = 9,
            Notes = new System.Collections.Generic.List<Note>
            {
                new Note { NoteId = "B1", MeasureNumber = 9, NoteIndexInMeasure = 0, TargetPitchStep = 0, DurationBeats = 0.25f },
                new Note { NoteId = "B2", MeasureNumber = 9, NoteIndexInMeasure = 1, TargetPitchStep = 0, DurationBeats = 0.75f },
                new Note { NoteId = "BR1", MeasureNumber = 9, NoteIndexInMeasure = 2, IsRest = true, DurationBeats = 1f },
                new Note { NoteId = "B3", MeasureNumber = 9, NoteIndexInMeasure = 3, TargetPitchStep = 7, DurationBeats = 1f / 6f },
                new Note { NoteId = "B4", MeasureNumber = 9, NoteIndexInMeasure = 4, TargetPitchStep = 5, DurationBeats = 1f / 3f },
                new Note { NoteId = "B5", MeasureNumber = 9, NoteIndexInMeasure = 5, TargetPitchStep = 2, DurationBeats = 0.5f },
                new Note { NoteId = "BR2", MeasureNumber = 9, NoteIndexInMeasure = 6, IsRest = true, DurationBeats = 1f }
            }
        };

        bool matched = RepeatMatcher.TryMatch(
            source, target, out var mapping, out var error);

        Assert.IsTrue(matched, error);
        Assert.IsNotNull(mapping);
        Assert.AreEqual("TEMP_A", mapping.SourceSectionId);
        Assert.AreEqual("TEMP_B", mapping.TargetSectionId);
    }

    [Test]
    public void InvalidNote_ReportsSongAndSection()
    {
        var source = CreateSection("TEMP_A", 1, 0, 7);
        var target = CreateSection("TEMP_B", 9, 0, 7);

        target.Notes[0].DurationBeats = -1f;

        bool matched = RepeatMatcher.TryMatch(
            source, target, out var mapping, out var error);

        Assert.IsFalse(matched);
        Assert.IsNull(mapping);
        StringAssert.Contains("TEMP_SONG_01", error);
        StringAssert.Contains("TEMP_B", error);
    }

    [Test]
    public void OverlappingMeasures_RejectsMapping()
    {
        var source = CreateSection("TEMP_A", 1, 0, 7);
        var target = CreateSection("TEMP_B", 1, 0, 7);

        bool matched = RepeatMatcher.TryMatch(
            source, target, out var mapping, out _);

        Assert.IsFalse(matched);
        Assert.IsNull(mapping);
    }

    [Test]
    public void NaNDuration_RejectsMapping()
    {
        var source = CreateSection("TEMP_A", 1, 0, 7);
        var target = CreateSection("TEMP_B", 9, 0, 7);

        target.Notes[0].DurationBeats = float.NaN;

        bool matched = RepeatMatcher.TryMatch(
            source, target, out var mapping, out var error);

        Assert.IsFalse(matched);
        Assert.IsNull(mapping);
        StringAssert.Contains("TEMP_SONG_01", error);
        StringAssert.Contains("TEMP_B", error);
    }

    [Test]
    public void InfinityDuration_RejectsMapping()
    {
        var source = CreateSection("TEMP_A", 1, 0, 7);
        var target = CreateSection("TEMP_B", 9, 0, 7);

        target.Notes[0].DurationBeats = float.PositiveInfinity;

        bool matched = RepeatMatcher.TryMatch(
            source, target, out var mapping, out var error);

        Assert.IsFalse(matched);
        Assert.IsNull(mapping);
    }

    [Test]
    public void DuplicateTarget_RejectsMappings()
    {
        var mappings = new[]
        {
        new RepeatMapping
        {
            SongId = "TEMP_SONG_01",
            SourceSectionId = "A",
            TargetSectionId = "C",
            SourceStartMeasure = 1,
            SourceEndMeasure = 4,
            TargetStartMeasure = 9,
            TargetEndMeasure = 12
        },
        new RepeatMapping
        {
            SongId = "TEMP_SONG_01",
            SourceSectionId = "B",
            TargetSectionId = "C",
            SourceStartMeasure = 5,
            SourceEndMeasure = 8,
            TargetStartMeasure = 9,
            TargetEndMeasure = 12
        }
    };

        bool valid = RepeatMappingValidator.TryValidate(
            mappings, out var error);

        Assert.IsFalse(valid);
        StringAssert.Contains("TEMP_SONG_01", error);
    }

    [Test]
    public void NestedReference_RejectsMappings()
    {
        var mappings = new[]
        {
        new RepeatMapping
        {
            SongId = "TEMP_SONG_01",
            SourceSectionId = "A",
            TargetSectionId = "B",
            SourceStartMeasure = 1,
            SourceEndMeasure = 4,
            TargetStartMeasure = 5,
            TargetEndMeasure = 8
        },
        new RepeatMapping
        {
            SongId = "TEMP_SONG_01",
            SourceSectionId = "B",
            TargetSectionId = "C",
            SourceStartMeasure = 5,
            SourceEndMeasure = 8,
            TargetStartMeasure = 9,
            TargetEndMeasure = 12
        }
    };

        bool valid = RepeatMappingValidator.TryValidate(
            mappings, out var error);

        Assert.IsFalse(valid);
        Assert.IsNotNull(error);
    }

    [Test]
    public void IndependentMappings_AreValid()
    {
        var mappings = new[]
        {
        new RepeatMapping
        {
            SongId = "TEMP_SONG_01",
            SourceSectionId = "A",
            TargetSectionId = "C",
            SourceStartMeasure = 1,
            SourceEndMeasure = 2,
            TargetStartMeasure = 5,
            TargetEndMeasure = 6
        },
        new RepeatMapping
        {
            SongId = "TEMP_SONG_01",
            SourceSectionId = "B",
            TargetSectionId = "D",
            SourceStartMeasure = 3,
            SourceEndMeasure = 4,
            TargetStartMeasure = 7,
            TargetEndMeasure = 8
        }
    };

        bool valid = RepeatMappingValidator.TryValidate(
            mappings, out var error);

        Assert.IsTrue(valid, error);
    }
}

