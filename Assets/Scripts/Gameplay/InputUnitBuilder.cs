using System;
using System.Collections.Generic;
using System.Linq;
using UtaNoUtakeiko.Data;

namespace UtaNoUtakeiko.Gameplay
{
    // Converts a PlayPlan into player actions, retaining original NoteIds for vocal playback.
    // Does not change the existing runtime path until the follow-up integration is complete.
    public static class InputUnitBuilder
    {
        public const float TimeScale = 0.7f;

        public static bool TryBuild(
            SongChart chart,
            PlayPlan playPlan,
            out List<InputUnit> units,
            out string error)
        {
            units = null;
            error = null;

            if (chart == null || chart.Bpm <= 0 ||
                chart.Sections == null || playPlan == null ||
                playPlan.SectionIds == null || playPlan.Notes == null ||
                chart.SongId != playPlan.SongId)
            {
                error = "入力譜面データが不正です。";
                return false;
            }

            var sections = chart.Sections.ToDictionary(s => s.SectionId);
            var playableIds = new HashSet<string>(playPlan.Notes.Select(n => n.NoteId));
            var result = new List<InputUnit>();
            var consumed = new HashSet<string>();
            double currentBeat = 0;
            int previousMeasure = -1;
            bool lastWasRest = true;

            foreach (string sectionId in playPlan.SectionIds)
            {
                if (!sections.TryGetValue(sectionId, out var section) ||
                    section == null || section.Notes == null)
                {
                    error = "入力対象区間が見つかりません: " + sectionId;
                    return false;
                }

                // Repeated sections omitted from PlayPlan have no input time.
                // A gap in source measures must never be joined as one hold.
                if (previousMeasure >= 0 && section.StartMeasure != previousMeasure + 1)
                    lastWasRest = true;

                foreach (var note in section.Notes
                    .OrderBy(n => n.MeasureNumber)
                    .ThenBy(n => n.NoteIndexInMeasure))
                {
                    if (note == null || note.DurationBeats <= 0f ||
                        float.IsNaN(note.DurationBeats) ||
                        float.IsInfinity(note.DurationBeats) ||
                        (!note.IsRest && note.TargetPitchStep < 0))
                    {
                        error = "不正な音符データがあります。";
                        return false;
                    }

                    double startBeat = currentBeat;
                    currentBeat += note.DurationBeats;
                    float start = (float)(startBeat * 60.0 / chart.Bpm * TimeScale);
                    float end = (float)(currentBeat * 60.0 / chart.Bpm * TimeScale);

                    if (note.IsRest)
                    {
                        lastWasRest = true;
                        continue;
                    }

                    if (!playableIds.Contains(note.NoteId) ||
                        !consumed.Add(note.NoteId))
                    {
                        error = "PlayPlanと元音符の対応が不正です: " + note.NoteId;
                        return false;
                    }

                    InputUnit previous = result.Count > 0 ? result[result.Count - 1] : null;
                    bool merge = !lastWasRest && previous != null &&
                        previous.TargetPitchStep == note.TargetPitchStep &&
                        Math.Abs(previous.EndSeconds - start) < 0.001f;

                    if (merge)
                    {
                        previous.SourceNoteIds.Add(note.NoteId);
                        previous.EndSeconds = end;
                        previous.IsHold = true;
                        previous.HoldDurationSeconds = previous.SourceNoteIds.Count;
                    }
                    else
                    {
                        result.Add(new InputUnit
                        {
                            UnitId = note.NoteId,
                            TargetPitchStep = note.TargetPitchStep,
                            StartSeconds = start,
                            EndSeconds = end,
                            IsHold = false,
                            HoldDurationSeconds = 1f,
                            SourceNoteIds = new List<string> { note.NoteId }
                        });
                    }

                    lastWasRest = false;
                }

                previousMeasure = section.EndMeasure;
            }

            if (consumed.Count != playableIds.Count || result.Count == 0)
            {
                error = "入力対象音符数が一致しません。";
                return false;
            }

            units = result;
            return true;
        }
    }
}
