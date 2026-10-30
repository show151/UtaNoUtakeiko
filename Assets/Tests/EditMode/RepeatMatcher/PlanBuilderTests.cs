using System.Collections.Generic;
using NUnit.Framework;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class PlanBuilderTests
    {
        private static Section CreateSection(
            string id, int measure, int pitch)
        {
            return new Section
            {
                SongId = "TEST_SONG",
                SectionId = id,
                StartMeasure = measure,
                EndMeasure = measure,
                Notes = new List<Note>
                {
                    new Note
                    {
                        NoteId = id + "_N1",
                        MeasureNumber = measure,
                        NoteIndexInMeasure = 0,
                        TargetPitchStep = pitch,
                        DurationBeats = 1f
                    }
                }
            };
        }

        [Test]
        public void RepeatedA_IsPlayedOnce_ButPlayedBackTwice()
        {
            var a1 = CreateSection("A1", 1, 5);
            var b = CreateSection("B", 2, 7);
            var a2 = CreateSection("A2", 3, 5);

            bool matched = RepeatMatcher.TryMatch(
                a1, a2, out var mapping, out string matchError);

            Assert.IsTrue(matched, matchError);

            bool success = PlanBuilder.TryBuild(
                new List<Section> { a1, b, a2 },
                new List<RepeatMapping> { mapping },
                out var playPlan,
                out var playbackPlan,
                out string error);

            Assert.IsTrue(success, error);

            // 入力はA+Bだけ
            CollectionAssert.AreEqual(
                new[] { "A1", "B" },
                playPlan.SectionIds);

            Assert.AreEqual(2, playPlan.Notes.Count);

            // 再生はA+B+A
            Assert.AreEqual(3, playbackPlan.Sections.Count);

            CollectionAssert.AreEqual(
                new[] { "A1", "B", "A2" },
                new[]
                {
                    playbackPlan.Sections[0].SectionId,
                    playbackPlan.Sections[1].SectionId,
                    playbackPlan.Sections[2].SectionId
                });

            // 最後のAは最初のAの入力結果を参照
            Assert.IsTrue(playbackPlan.Sections[2].IsRepeated);
            Assert.AreEqual(
                "A1_N1",
                playbackPlan.Sections[2].SourceNoteIds[0]);
        }

        [Test]
        public void DifferentPitch_IsNotSkipped()
        {
            var a1 = CreateSection("A1", 1, 5);
            var b = CreateSection("B", 2, 7);
            var a2 = CreateSection("A2", 3, 6);

            // 不一致の区間を繰り返し候補として指定
            var mapping = new RepeatMapping
            {
                SongId = "TEST_SONG",
                SourceSectionId = "A1",
                TargetSectionId = "A2",
                SourceStartMeasure = 1,
                SourceEndMeasure = 1,
                TargetStartMeasure = 3,
                TargetEndMeasure = 3
            };

            bool success = PlanBuilder.TryBuild(
                new List<Section> { a1, b, a2 },
                new List<RepeatMapping> { mapping },
                out var playPlan,
                out var playbackPlan,
                out string error);

            Assert.IsTrue(success, error);

            // 音程が違うのでA2も入力対象
            CollectionAssert.AreEqual(
                new[] { "A1", "B", "A2" },
                playPlan.SectionIds);

            Assert.AreEqual(3, playPlan.Notes.Count);
            Assert.AreEqual(3, playbackPlan.Sections.Count);

            // A2は独立した入力結果を参照
            Assert.IsFalse(playbackPlan.Sections[2].IsRepeated);
            Assert.AreEqual(
                "A2_N1",
                playbackPlan.Sections[2].SourceNoteIds[0]);
        }

        [Test]
        public void TwinkleTwinkle_TwelveMeasures_UsesSixMeasuresOfInput()
        {
            // 2小節を1区間としたテスト用データ
            Section MakeTwoMeasureSection(
                string id, int startMeasure, int pitch)
            {
                return new Section
                {
                    SongId = "TEST_SONG",
                    SectionId = id,
                    StartMeasure = startMeasure,
                    EndMeasure = startMeasure + 1,
                    Notes = new List<Note>
            {
                new Note
                {
                    NoteId = id + "_N1",
                    MeasureNumber = startMeasure,
                    NoteIndexInMeasure = 0,
                    TargetPitchStep = pitch,
                    DurationBeats = 1f
                },
                new Note
                {
                    NoteId = id + "_N2",
                    MeasureNumber = startMeasure + 1,
                    NoteIndexInMeasure = 0,
                    TargetPitchStep = pitch + 1,
                    DurationBeats = 1f
                }
            }
                };
            }

            var a1 = MakeTwoMeasureSection("A1", 1, 5);
            var b1 = MakeTwoMeasureSection("B1", 3, 7);
            var c1 = MakeTwoMeasureSection("C1", 5, 9);
            var c2 = MakeTwoMeasureSection("C2", 7, 9);
            var a2 = MakeTwoMeasureSection("A2", 9, 5);
            var b2 = MakeTwoMeasureSection("B2", 11, 7);

            var sections = new List<Section>
            {
                a1, b1, c1, c2, a2, b2
            };

                    var mappings = new List<RepeatMapping>();

                    foreach (var pair in new[]
                    {
                (source: c1, target: c2),
                (source: a1, target: a2),
                (source: b1, target: b2)
            })
            {
                Assert.IsTrue(
                    RepeatMatcher.TryMatch(
                        pair.source,
                        pair.target,
                        out var mapping,
                        out string matchError),
                    matchError);

                mappings.Add(mapping);
            }

            bool success = PlanBuilder.TryBuild(
                sections,
                mappings,
                out var playPlan,
                out var playbackPlan,
                out string error);

            Assert.IsTrue(success, error);

            // 入力対象は最初の6小節（3区間）
            CollectionAssert.AreEqual(
                new[] { "A1", "B1", "C1" },
                playPlan.SectionIds);

            Assert.AreEqual(6, playPlan.Notes.Count);

            // 歌唱対象は12小節（6区間）
            Assert.AreEqual(6, playbackPlan.Sections.Count);

            CollectionAssert.AreEqual(
                new[] { "A1", "B1", "C1", "C2", "A2", "B2" },
                playbackPlan.Sections.ConvertAll(s => s.SectionId));

            // 繰り返し部分の入力元を確認
            CollectionAssert.AreEqual(
                new[] { "C1_N1", "C1_N2" },
                playbackPlan.Sections[3].SourceNoteIds);

            CollectionAssert.AreEqual(
                new[] { "A1_N1", "A1_N2" },
                playbackPlan.Sections[4].SourceNoteIds);

            CollectionAssert.AreEqual(
                new[] { "B1_N1", "B1_N2" },
                playbackPlan.Sections[5].SourceNoteIds);

            Assert.IsTrue(playbackPlan.Sections[3].IsRepeated);
            Assert.IsTrue(playbackPlan.Sections[4].IsRepeated);
            Assert.IsTrue(playbackPlan.Sections[5].IsRepeated);
        }

        [Test]
        public void RepeatedSection_WithRest_PreservesRestAndReusesInput()
        {
            var a1 = CreateSection("A1", 1, 5);
            var a2 = CreateSection("A2", 2, 5);

            a1.Notes.Add(new Note
            {
                NoteId = "A1_REST",
                MeasureNumber = 1,
                NoteIndexInMeasure = 1,
                IsRest = true,
                DurationBeats = 1f
            });

            a2.Notes.Add(new Note
            {
                NoteId = "A2_REST",
                MeasureNumber = 2,
                NoteIndexInMeasure = 1,
                IsRest = true,
                DurationBeats = 1f
            });

            Assert.IsTrue(
                RepeatMatcher.TryMatch(
                    a1, a2,
                    out var mapping,
                    out string matchError),
                matchError);

            bool success = PlanBuilder.TryBuild(
                new List<Section> { a1, a2 },
                new List<RepeatMapping> { mapping },
                out var playPlan,
                out var playbackPlan,
                out string error);

            Assert.IsTrue(success, error);

            // 入力対象は休符を除いたA1の1音のみ
            Assert.AreEqual(1, playPlan.Notes.Count);
            Assert.AreEqual("A1_N1", playPlan.Notes[0].NoteId);

            // 再生対象はA1・A2の両区間
            Assert.AreEqual(2, playbackPlan.Sections.Count);

            var repeated = playbackPlan.Sections[1];

            Assert.AreEqual(2, repeated.Notes.Count);

            // 音符は元の入力結果を再利用
            Assert.AreEqual("A1_N1", repeated.SourceNoteIds[0]);

            // 休符は再生位置を維持し、入力結果を参照しない
            Assert.IsNull(repeated.SourceNoteIds[1]);
        }

        [Test]
        public void MappingToUnknownSection_FailsWithoutProducingPlans()
        {
            var a1 = CreateSection("A1", 1, 5);

            var invalidMapping = new RepeatMapping
            {
                SongId = "TEST_SONG",
                SourceSectionId = "A1",
                TargetSectionId = "UNKNOWN",
                SourceStartMeasure = 1,
                SourceEndMeasure = 1,
                TargetStartMeasure = 2,
                TargetEndMeasure = 2
            };

            bool success = PlanBuilder.TryBuild(
                new List<Section> { a1 },
                new List<RepeatMapping> { invalidMapping },
                out var playPlan,
                out var playbackPlan,
                out string error);

            Assert.IsFalse(success);
            Assert.IsNull(playPlan);
            Assert.IsNull(playbackPlan);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
        }

        [Test]
        public void OverlappingSections_AreRejected()
        {
            var a = CreateSection("A", 1, 5);
            var b = CreateSection("B", 1, 7);

            bool success = PlanBuilder.TryBuild(
                new List<Section> { a, b },
                new List<RepeatMapping>(),
                out var playPlan,
                out var playbackPlan,
                out string error);

            Assert.IsFalse(success);
            Assert.IsNull(playPlan);
            Assert.IsNull(playbackPlan);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
        }

        [Test]
        public void DuplicateNoteIdsAcrossSections_AreRejected()
        {
            var a = CreateSection("A", 1, 5);
            var b = CreateSection("B", 2, 7);

            // 異なる区間に同じNoteIdを設定
            b.Notes[0].NoteId = a.Notes[0].NoteId;

            bool success = PlanBuilder.TryBuild(
                new List<Section> { a, b },
                new List<RepeatMapping>(),
                out var playPlan,
                out var playbackPlan,
                out string error);

            Assert.IsFalse(success);
            Assert.IsNull(playPlan);
            Assert.IsNull(playbackPlan);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
        }
    }
}