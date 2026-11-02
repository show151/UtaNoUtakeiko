
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace Uta.GameFlow
{
    public static class VocalPlaybackComposer
    {
        private class Placement
        {
            public AudioClip Clip;
            public double StartSeconds;
            public double DurationSeconds;
        }

        public static AudioClip Compose(
            SongChart chart,
            PlaybackPlan plan,
            ScoreResult score)
        {
            if (chart == null || plan == null || score == null ||
                chart.Bpm <= 0 ||
                chart.TimeSignatureDenominator <= 0 ||
                chart.Sections == null ||
                chart.Sections.Count == 0 ||
                plan.Sections == null ||
                score.UnitResults == null ||
                chart.SongId != plan.SongId ||
                chart.SongId != score.SongId)
            {
                throw new ArgumentException("歌唱生成データが不正です");
            }

            double beatsPerMeasure =
                chart.TimeSignatureNumerator * 4.0 /
                chart.TimeSignatureDenominator;

            double secondsPerBeat = 60.0 / chart.Bpm;

            double totalSeconds =
                chart.Sections.Max(s => s.EndMeasure) *
                beatsPerMeasure * secondsPerBeat;

            var results = new Dictionary<string, UnitResult>();

            foreach (var unit in score.UnitResults)
            {
                if (unit == null ||
                    string.IsNullOrEmpty(unit.UnitId) ||
                    !results.TryAdd(unit.UnitId, unit))
                {
                    throw new ArgumentException("入力結果に重複または不正なIDがあります");
                }
            }

            var placements = new List<Placement>();

            // 再生先の各音符に対して、元入力の音程を参照
            foreach (var section in plan.Sections)
            {
                if (section.Notes == null ||
                    section.SourceNoteIds == null ||
                    section.Notes.Count != section.SourceNoteIds.Count)
                {
                    throw new ArgumentException("PlaybackPlanの音符対応が不正です");
                }

                var indices = Enumerable
                    .Range(0, section.Notes.Count)
                    .OrderBy(i => section.Notes[i].MeasureNumber)
                    .ThenBy(i => section.Notes[i].NoteIndexInMeasure);

                int currentMeasure = -1;
                double elapsedBeats = 0;

                foreach (int i in indices)
                {
                    Note note = section.Notes[i];

                    if (note.MeasureNumber != currentMeasure)
                    {
                        currentMeasure = note.MeasureNumber;
                        elapsedBeats = 0;
                    }

                    double startBeat =
                        (note.MeasureNumber - 1) * beatsPerMeasure
                        + elapsedBeats;

                    elapsedBeats += note.DurationBeats;

                    if (elapsedBeats > beatsPerMeasure + 0.001)
                    {
                        throw new ArgumentException("小節の拍数が超過しています");
                    }

                    if (note.IsRest)
                        continue;

                    string sourceId = section.SourceNoteIds[i];

                    if (string.IsNullOrEmpty(sourceId))
                        throw new ArgumentException("入力元NoteIdがありません");

                    // 未入力・時間切れの音符は無音にする
                    if (!results.TryGetValue(sourceId, out var unit) ||
                        unit.InputState != InputState.Completed ||
                        !unit.CapturedPitchStep.HasValue)
                    {
                        continue;
                    }

                    int pitch = unit.CapturedPitchStep.Value;

                    if (pitch < 0 || pitch > 9)
                        throw new ArgumentException("音程が範囲外です");

                    // WAVは再生先NoteIdで選択する
                    // 入力音程は繰り返し元のNoteIdで参照済み
                    string path =
                        $"VocalBank/{chart.SongId}/" +
                        $"{note.NoteId}_p{pitch}";

                    AudioClip clip = Resources.Load<AudioClip>(path);

                    if (clip == null)
                        throw new InvalidOperationException(
                            $"歌唱素材がありません: {path}"
                        );

                    placements.Add(new Placement
                    {
                        Clip = clip,
                        StartSeconds = startBeat * secondsPerBeat,
                        DurationSeconds =
                            note.DurationBeats * secondsPerBeat
                    });
                }
            }

            // 入力がゼロの場合も無音のAudioClipを生成
            int frequency = placements.Count > 0
                ? placements[0].Clip.frequency
                : AudioSettings.outputSampleRate;

            int channels = placements.Count > 0
                ? placements[0].Clip.channels
                : 1;

            int totalFrames = (int)Math.Round(
                totalSeconds * frequency
            );

            float[] samples =
                new float[totalFrames * channels];

            foreach (var placement in placements)
            {
                AudioClip clip = placement.Clip;

                if (clip.frequency != frequency ||
                    clip.channels != channels)
                {
                    throw new InvalidOperationException(
                        "音声素材のサンプルレートまたはチャンネル数が不一致"
                    );
                }

                int start = (int)Math.Round(
                    placement.StartSeconds * frequency
                );

                int end = (int)Math.Round(
                    (placement.StartSeconds +
                     placement.DurationSeconds) * frequency
                );

                int expectedFrames = end - start;

                if (start < 0 || end > totalFrames ||
                    Math.Abs(clip.samples - expectedFrames) > 2)
                {
                    throw new InvalidOperationException(
                        $"音声の長さが不一致: {clip.name}"
                    );
                }

                float[] clipSamples =
                    new float[clip.samples * channels];

                if (!clip.GetData(clipSamples, 0))
                    throw new InvalidOperationException(
                        $"音声の読み込みに失敗: {clip.name}"
                    );

                int copyFrames = Math.Min(
                    clip.samples, expectedFrames
                );

                Array.Copy(
                    clipSamples,
                    0,
                    samples,
                    start * channels,
                    copyFrames * channels
                );
            }

            AudioClip output = AudioClip.Create(
                "GeneratedVocal",
                totalFrames,
                channels,
                frequency,
                false
            );

            if (!output.SetData(samples, 0))
            {
                UnityEngine.Object.Destroy(output);
                throw new InvalidOperationException(
                    "歌唱AudioClipの生成に失敗しました"
                );
            }

            Debug.Log(
                $"歌唱生成成功: {placements.Count}音符 / " +
                $"{output.length:F2}秒"
            );

            return output;
        }
    }
}