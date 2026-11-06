using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uta.GameFlow;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class SenbonChartTests
    {
        [Test]
        public void ChartMapsAllOccurrencesToInputAndHasCompleteAudioBank()
        {
            var asset = Resources.Load<TextAsset>("SongCharts/senbon_hard");
            Assert.IsNotNull(asset);
            var chart = JsonUtility.FromJson<SongChart>(asset.text);
            Assert.AreEqual("HARD", chart.DifficultyId);
            Assert.AreEqual(15, chart.PitchStepCount);
            Assert.AreEqual(1, chart.PickupBeats);
            Assert.IsTrue(PlanBuilder.TryBuild(chart.Sections, chart.RepeatMappings,
                out var play, out var playback, out var error), error);
            Assert.AreEqual(27, play.Notes.Count);
            Assert.AreEqual(94, playback.Sections.Sum(section => section.Notes.Count));
            Assert.IsTrue(InputUnitBuilder.TryBuild(chart, play, out var units, out error), error);
            Assert.AreEqual(4, units.Count(unit => !string.IsNullOrEmpty(unit.StairGroupId)));
            foreach (var note in chart.Sections.SelectMany(section => section.Notes))
                for (int pitch = 0; pitch < chart.PitchStepCount; pitch++)
                    Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath,
                        "Resources", "VocalBank", chart.SongId, $"{note.NoteId}_p{pitch}.ogg")),
                        $"Missing audio for {note.NoteId}, pitch {pitch}");
        }

        [Test]
        public void PickupBeginsImmediatelyAndSecondMeasureStartsAfterOneBeat()
        {
            var chart = new SongChart { TimeSignatureNumerator = 4, TimeSignatureDenominator = 4, PickupBeats = 1 };
            Assert.AreEqual(0d, SongTiming.StartBeat(chart, 1, 0));
            Assert.AreEqual(1d, SongTiming.StartBeat(chart, 2, 0));
            Assert.AreEqual(65d, SongTiming.TotalBeats(chart, 17));
        }

        [TestCase(0)]
        [TestCase(14)]
        public void ExtremeInputPitchesComposeTheWholeSong(int pitch)
        {
            var chart = JsonUtility.FromJson<SongChart>(Resources.Load<TextAsset>("SongCharts/senbon_hard").text);
            Assert.IsTrue(PlanBuilder.TryBuild(chart.Sections, chart.RepeatMappings,
                out var play, out var playback, out var error), error);
            var result = new ScoreResult { SongId = chart.SongId };
            foreach (var note in play.Notes)
                result.UnitResults.Add(new UnitResult { UnitId = note.NoteId,
                    InputState = InputState.Completed, CapturedPitchStep = pitch });
            var clip = VocalPlaybackComposer.Compose(chart, playback, result);
            try
            {
                Assert.IsNotNull(clip);
                Assert.AreEqual(65f * 60f / 154f, clip.length, 0.002f);
            }
            finally { if (clip != null) Object.DestroyImmediate(clip); }
        }
    }
}
