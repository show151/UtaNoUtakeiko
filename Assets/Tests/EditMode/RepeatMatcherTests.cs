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
