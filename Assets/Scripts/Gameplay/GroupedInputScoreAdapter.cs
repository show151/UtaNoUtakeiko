using System;
using System.Collections.Generic;
using UnityEngine;
using UtaNoUtakeiko.Data;

namespace UtaNoUtakeiko.Gameplay
{
    // Adapts the new grouped input units to the existing scoring/audio contracts.
    // Original SongChart, PlayPlan and per-note vocal assets are not mutated.
    public static class GroupedInputScoreAdapter
    {
        public static ScoreResult Calculate(
            PlayPlan originalPlan,
            IReadOnlyList<InputUnit> units,
            IReadOnlyList<TimedInputTrialResult> trialResults,
            ScorePolicy policy,
            Difficulty difficulty,
            int minPitchStep,
            int maxPitchStep)
        {
            if (originalPlan == null || originalPlan.Notes == null ||
                units == null || units.Count == 0 || trialResults == null ||
                policy == null || maxPitchStep <= minPitchStep)
                throw new ArgumentException("Grouped scoring input is invalid.");

            ValidateSourceMapping(originalPlan, units);

            var byId = new Dictionary<string, TimedInputTrialResult>();
            foreach (var trial in trialResults)
            {
                if (trial == null || string.IsNullOrWhiteSpace(trial.UnitId) ||
                    !byId.TryAdd(trial.UnitId, trial))
                    throw new ArgumentException("Duplicate or invalid trial result.");
            }
            if (byId.Count != units.Count)
                throw new ArgumentException("Not every input unit was resolved.");

            var scoringPlan = new PlayPlan { SongId = originalPlan.SongId };
            var unitResults = new List<UnitResult>();
            bool hasTimedOutInput = false;

            foreach (InputUnit unit in units)
            {
                if (!byId.TryGetValue(unit.UnitId, out var trial))
                    throw new ArgumentException("Missing result for " + unit.UnitId);

                scoringPlan.Notes.Add(new Note
                {
                    NoteId = unit.UnitId,
                    TargetPitchStep = unit.TargetPitchStep,
                    DurationBeats = 1f,
                    IsHold = unit.IsHold,
                    IsRest = false
                });

                if (trial.TimedOut)
                {
                    if (trial.CapturedPosition.HasValue)
                        throw new ArgumentException("A missed unit cannot carry pitch.");
                    hasTimedOutInput = true;
                    unitResults.Add(new UnitResult
                    {
                        UnitId = unit.UnitId,
                        InputState = InputState.TimedOut
                    });
                    continue;
                }

                if (!trial.CapturedPosition.HasValue ||
                    float.IsNaN(trial.CapturedPosition.Value) ||
                    float.IsInfinity(trial.CapturedPosition.Value) ||
                    trial.CapturedPosition.Value < 0f ||
                    trial.CapturedPosition.Value > 1f ||
                    !Enum.TryParse(trial.PitchJudgment, out JudgmentType judgment) ||
                    !Enum.IsDefined(typeof(JudgmentType), judgment))
                    throw new ArgumentException("Invalid captured judgment: " + unit.UnitId);

                if (unit.IsHold != trial.HoldSucceeded.HasValue)
                    throw new ArgumentException("Hold result does not match the unit.");

                int capturedPitchStep = Mathf.RoundToInt(Mathf.Lerp(
                    minPitchStep, maxPitchStep, trial.CapturedPosition.Value));

                unitResults.Add(new UnitResult
                {
                    UnitId = unit.UnitId,
                    InputState = InputState.Completed,
                    CapturedPitchStep = capturedPitchStep,
                    Judgment = judgment,
                    HoldJudgment = unit.IsHold
                        ? (trial.HoldSucceeded.Value
                            ? HoldJudgmentType.Perfect : HoldJudgmentType.Miss)
                        : (HoldJudgmentType?)null
                });
            }

            // An auto-missed unit reached its input deadline; treat it as a
            // timed-out input under the existing ScoreCalculator contract.
            return ScoreCalculator.Calculate(
                scoringPlan, unitResults, policy, difficulty, hasTimedOutInput);
        }

        // For the original VocalPlaybackComposer, expand one result to all
        // original source NoteIds. This is an audio-only view of the score.
        public static ScoreResult ExpandForVocalPlayback(
            ScoreResult groupedScore,
            IReadOnlyList<InputUnit> units)
        {
            if (groupedScore == null || groupedScore.UnitResults == null ||
                units == null || units.Count == 0)
                throw new ArgumentException("Grouped vocal data is invalid.");

            var resultsByUnit = new Dictionary<string, UnitResult>();
            foreach (UnitResult result in groupedScore.UnitResults)
            {
                if (result == null || string.IsNullOrWhiteSpace(result.UnitId) ||
                    !resultsByUnit.TryAdd(result.UnitId, result))
                    throw new ArgumentException("Invalid grouped score result.");
            }
            if (resultsByUnit.Count != units.Count)
                throw new ArgumentException("Grouped score result count mismatch.");

            var expanded = new ScoreResult
            {
                SongId = groupedScore.SongId,
                Difficulty = groupedScore.Difficulty,
                TimedOut = groupedScore.TimedOut,
                Score = groupedScore.Score,
                MaxScore = groupedScore.MaxScore,
                PerfectCount = groupedScore.PerfectCount,
                GreatCount = groupedScore.GreatCount,
                GoodCount = groupedScore.GoodCount,
                BadCount = groupedScore.BadCount,
                MissCount = groupedScore.MissCount
            };

            var noteIds = new HashSet<string>();
            string stairGroup = null;
            int? firstStairPitch = null;
            foreach (InputUnit unit in units)
            {
                if (unit == null || !resultsByUnit.TryGetValue(unit.UnitId, out var result) ||
                    unit.SourceNoteIds == null || unit.SourceNoteIds.Count == 0)
                    throw new ArgumentException("Input unit mapping is invalid.");

                // Change only vocal playback; retain the actual scoring results.
                if (string.IsNullOrEmpty(unit.StairGroupId) || unit.StairGroupId != stairGroup)
                {
                    stairGroup = unit.StairGroupId;
                    firstStairPitch = null;
                    if (!string.IsNullOrEmpty(stairGroup) && result.InputState == InputState.Completed)
                        firstStairPitch = result.CapturedPitchStep;
                }
                bool useFirstPitch = !string.IsNullOrEmpty(stairGroup) &&
                    firstStairPitch.HasValue && result.InputState == InputState.TimedOut;

                foreach (string sourceNoteId in unit.SourceNoteIds)
                {
                    if (string.IsNullOrWhiteSpace(sourceNoteId) || !noteIds.Add(sourceNoteId))
                        throw new ArgumentException("Duplicate source NoteId.");

                    expanded.UnitResults.Add(new UnitResult
                    {
                        UnitId = sourceNoteId,
                        InputState = useFirstPitch ? InputState.Completed : result.InputState,
                        CapturedPitchStep = useFirstPitch ? firstStairPitch : result.CapturedPitchStep,
                        Judgment = result.Judgment,
                        HoldJudgment = null,
                        BaseScore = 0,
                        HoldScore = 0
                    });
                }
            }
            return expanded;
        }

        private static void ValidateSourceMapping(
            PlayPlan plan, IReadOnlyList<InputUnit> units)
        {
            var expected = new HashSet<string>();
            foreach (Note note in plan.Notes)
            {
                if (note == null || note.IsRest ||
                    string.IsNullOrWhiteSpace(note.NoteId) ||
                    !expected.Add(note.NoteId))
                    throw new ArgumentException("Original PlayPlan is invalid.");
            }

            var unitIds = new HashSet<string>();
            var sources = new HashSet<string>();
            foreach (InputUnit unit in units)
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.UnitId) ||
                    !unitIds.Add(unit.UnitId) || unit.SourceNoteIds == null ||
                    unit.SourceNoteIds.Count == 0 ||
                    unit.IsHold != (unit.SourceNoteIds.Count >= 2))
                    throw new ArgumentException("Grouped InputUnit is invalid.");

                foreach (string source in unit.SourceNoteIds)
                {
                    if (string.IsNullOrWhiteSpace(source) ||
                        !expected.Contains(source) || !sources.Add(source))
                        throw new ArgumentException("Invalid grouped source mapping.");
                }
            }
            if (sources.Count != expected.Count)
                throw new ArgumentException("Grouped sources do not cover the original notes.");
        }
    }
}
