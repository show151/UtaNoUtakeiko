
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class SongChartImportTests
    {
        [Test]
        public void TwinkleEasy_JsonAndPlans_AreValid()
        {
            // 本番譜面の読み込み
            string path = Path.Combine(
                Application.dataPath,
                "Data",
                "Songs",
                "twinkle_easy.json"
            );

            Assert.IsTrue(File.Exists(path),
                "譜面JSONが見つかりません: " + path);

            string json = File.ReadAllText(path);
            SongChart chart = JsonUtility.FromJson<SongChart>(json);

            Assert.IsNotNull(chart);

            // 楽曲情報
            Assert.AreEqual("TWINKLE_TWINKLE", chart.SongId);
            Assert.AreEqual("EASY", chart.DifficultyId);
            Assert.AreEqual(100, chart.Bpm);
            Assert.AreEqual(4, chart.TimeSignatureNumerator);
            Assert.AreEqual(4, chart.TimeSignatureDenominator);

            // 譜面構造
            Assert.AreEqual(4, chart.Sections.Count);
            Assert.AreEqual(2, chart.RepeatMappings.Count);

            var allNotes = chart.Sections
                .SelectMany(s => s.Notes)
                .ToList();

            Assert.AreEqual(44, allNotes.Count);
            Assert.AreEqual(42, allNotes.Count(n => !n.IsRest));
            Assert.AreEqual(2, allNotes.Count(n => n.IsRest));
            Assert.AreEqual(6, allNotes.Count(n => n.IsHold));

            // 12小節 × 4拍 = 48拍
            Assert.AreEqual(
                48f,
                allNotes.Sum(n => n.DurationBeats),
                0.001f
            );

            // 既存のPlanBuilderで正式データを処理
            bool success = PlanBuilder.TryBuild(
                chart.Sections,
                chart.RepeatMappings,
                out PlayPlan playPlan,
                out PlaybackPlan playbackPlan,
                out string error
            );

            Assert.IsTrue(success, error);

            // 入力するのは1〜6小節の21音符
            Assert.AreEqual(21, playPlan.Notes.Count);

            // 歌唱対象は全12小節
            Assert.AreEqual(4, playbackPlan.Sections.Count);
            Assert.AreEqual(
                44,
                playbackPlan.Sections.Sum(s => s.Notes.Count)
            );

            // 繰り返し区間は2つ
            Assert.AreEqual(
                2,
                playbackPlan.Sections.Count(s => s.IsRepeated)
            );
        }

        [Test]
        public void TwinkleEasy_RepeatsAndMeasures_AreConsistent()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Data",
                "Songs",
                "twinkle_easy.json"
            );

            SongChart chart = JsonUtility.FromJson<SongChart>(
                File.ReadAllText(path)
            );

            var allNotes = chart.Sections
                .SelectMany(s => s.Notes)
                .ToList();

            // 全小節が4拍で構成されていることを確認
            for (int measure = 1; measure <= 12; measure++)
            {
                var notes = allNotes
                    .Where(n => n.MeasureNumber == measure)
                    .ToList();

                Assert.IsNotEmpty(notes, $"小節{measure}が空です");

                Assert.AreEqual(
                    4f,
                    notes.Sum(n => n.DurationBeats),
                    0.001f,
                    $"小節{measure}の拍数が不正です"
                );
            }

            bool success = PlanBuilder.TryBuild(
                chart.Sections,
                chart.RepeatMappings,
                out PlayPlan playPlan,
                out PlaybackPlan playbackPlan,
                out string error
            );

            Assert.IsTrue(success, error);

            // 繰り返し元の区間IDを確認
            Assert.AreEqual(
                "SECTION_B1",
                playbackPlan.Sections[2].SourceSectionId
            );

            Assert.AreEqual(
                "SECTION_A1",
                playbackPlan.Sections[3].SourceSectionId
            );

            foreach (var target in playbackPlan.Sections
                .Where(s => s.IsRepeated))
            {
                var source = playbackPlan.Sections.Single(
                    s => s.SectionId == target.SourceSectionId
                );

                var sourceNotes = source.Notes
                    .Where(n => !n.IsRest)
                    .ToList();

                Assert.AreEqual(
                    target.Notes.Count,
                    target.SourceNoteIds.Count
                );

                int sourceIndex = 0;

                for (int i = 0; i < target.Notes.Count; i++)
                {
                    var note = target.Notes[i];
                    string sourceNoteId = target.SourceNoteIds[i];

                    if (note.IsRest)
                    {
                        Assert.IsNull(sourceNoteId);
                        continue;
                    }

                    var original = sourceNotes[sourceIndex++];

                    // 正しい入力元NoteIdを参照する
                    Assert.AreEqual(original.NoteId, sourceNoteId);

                    // 実データ上の音程・音価・長押しも一致
                    Assert.AreEqual(
                        original.TargetPitchStep,
                        note.TargetPitchStep
                    );

                    Assert.AreEqual(
                        original.DurationBeats,
                        note.DurationBeats,
                        0.001f
                    );

                    Assert.AreEqual(original.IsHold, note.IsHold);

                    // 元の音符はPlayPlanに存在する
                    Assert.IsTrue(
                        playPlan.Notes.Any(n => n.NoteId == sourceNoteId)
                    );
                }

                Assert.AreEqual(sourceNotes.Count, sourceIndex);
            }
        }

        [Test]
        public void TwinkleEasy_DuplicateNoteId_IsRejected()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Data", "Songs", "twinkle_easy.json"
            );

            var chart = JsonUtility.FromJson<SongChart>(
                File.ReadAllText(path)
            );

            // 別区間の音符に同じIDを設定する
            chart.Sections[1].Notes[0].NoteId =
                chart.Sections[0].Notes[0].NoteId;

            bool success = PlanBuilder.TryBuild(
                chart.Sections,
                chart.RepeatMappings,
                out PlayPlan playPlan,
                out PlaybackPlan playbackPlan,
                out string error
            );

            Assert.IsFalse(success);
            Assert.IsNull(playPlan);
            Assert.IsNull(playbackPlan);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TwinkleEasy_DifferentRepeatPitch_IsNotSkipped()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Data", "Songs", "twinkle_easy.json"
            );

            var chart = JsonUtility.FromJson<SongChart>(
                File.ReadAllText(path)
            );

            // 7〜8小節の最初の音程を意図的に変更する
            var target = chart.Sections.Single(
                s => s.StartMeasure == 7
            );

            target.Notes.First(n => !n.IsRest).TargetPitchStep += 1;

            bool success = PlanBuilder.TryBuild(
                chart.Sections,
                chart.RepeatMappings,
                out PlayPlan playPlan,
                out PlaybackPlan playbackPlan,
                out string error
            );

            Assert.IsTrue(success, error);

            // 一致しない7〜8小節の7音が入力対象に追加される
            Assert.AreEqual(28, playPlan.Notes.Count);

            // 7〜8小節は繰り返し扱いされない
            var targetPlayback = playbackPlan.Sections.Single(
                s => s.SectionId == target.SectionId
            );

            Assert.IsFalse(targetPlayback.IsRepeated);

            // 9〜12小節の繰り返しはそのまま有効
            Assert.AreEqual(
                1,
                playbackPlan.Sections.Count(s => s.IsRepeated)
            );
        }

        [Test]
        public void TwinkleEasy_PitchesAndHolds_MatchSheet()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Data", "Songs", "twinkle_easy.json"
            );

            var chart = JsonUtility.FromJson<SongChart>(
                File.ReadAllText(path)
            );

            var notes = chart.Sections
                .SelectMany(s => s.Notes)
                .ToList();

            // C4=0、1半音=1ステップ
            int[][] expectedPitches =
            {
        new[] { 0, 0, 7, 7 }, // 1小節
        new[] { 9, 9, 7 },    // 2小節
        new[] { 5, 5, 4, 4 }, // 3小節
        new[] { 2, 2, 0 },    // 4小節
        new[] { 7, 7, 5, 5 }, // 5小節
        new[] { 4, 4, 2 },    // 6小節
        new[] { 7, 7, 5, 5 }, // 7小節
        new[] { 4, 4, 2 },    // 8小節
        new[] { 0, 0, 7, 7 }, // 9小節
        new[] { 9, 9, 7 },    // 10小節
        new[] { 5, 5, 4, 4 }, // 11小節
        new[] { 2, 2, 0 }     // 12小節
    };

            for (int measure = 1; measure <= 12; measure++)
            {
                var actual = notes
                    .Where(n => n.MeasureNumber == measure && !n.IsRest)
                    .OrderBy(n => n.NoteIndexInMeasure)
                    .Select(n => n.TargetPitchStep)
                    .ToArray();

                CollectionAssert.AreEqual(
                    expectedPitches[measure - 1],
                    actual,
                    $"小節{measure}の音程が違います"
                );
            }

            // 長押しは1.5拍または2拍の音符
            var holds = notes.Where(n => n.IsHold).ToList();

            Assert.AreEqual(6, holds.Count);

            foreach (var note in notes)
            {
                if (note.IsRest)
                {
                    Assert.IsFalse(note.IsHold);
                    continue;
                }

                Assert.AreEqual(
                    note.DurationBeats > 1f,
                    note.IsHold,
                    $"長押し設定が不正: {note.NoteId}"
                );

                if (note.IsHold)
                {
                    double seconds = note.DurationBeats * 60.0 / chart.Bpm;

                    Assert.IsTrue(
                        System.Math.Abs(seconds - 0.9) < 0.001 ||
                        System.Math.Abs(seconds - 1.2) < 0.001,
                        $"長押し時間が不正: {note.NoteId}"
                    );
                }
            }
        }
    }
}
