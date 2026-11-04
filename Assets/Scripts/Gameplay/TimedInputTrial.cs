using System;
using System.Collections.Generic;
using UtaNoUtakeiko.Data;

namespace UtaNoUtakeiko.Gameplay
{
    // Trial-only result. Not a ScoreResult; old scoring still uses raw NoteIds.
    public sealed class TimedInputTrialResult
    {
        public string UnitId;
        public string PitchJudgment;
        public float? CapturedPosition;
        public bool? HoldSucceeded;
        public float HeldSeconds;
        public bool TimedOut;
    }

    // Pure timeline bookkeeping for the Issue #53 interactive input trial.
    public sealed class TimedInputTrial
    {
        public const float ReleaseGraceSeconds = 0.12f;

        private readonly List<InputUnit> units;
        private readonly TimedInputTrialResult[] results;

        public event Action<TimedInputTrialResult> OnResolved;

        public int Count => units.Count;
        public int ResolvedCount { get; private set; }

        public TimedInputTrial(List<InputUnit> inputUnits)
        {
            if (inputUnits == null || inputUnits.Count == 0)
                throw new ArgumentException("Input units are empty.");

            units = new List<InputUnit>(inputUnits);
            results = new TimedInputTrialResult[units.Count];

            float previousEnd = 0f;
            for (int i = 0; i < units.Count; i++)
            {
                InputUnit unit = units[i];
                if (unit == null || string.IsNullOrWhiteSpace(unit.UnitId) ||
                    float.IsNaN(unit.StartSeconds) || float.IsNaN(unit.EndSeconds) ||
                    float.IsInfinity(unit.StartSeconds) ||
                    float.IsInfinity(unit.EndSeconds) ||
                    unit.StartSeconds < previousEnd - 0.001f ||
                    unit.EndSeconds <= unit.StartSeconds)
                {
                    throw new ArgumentException("Invalid input unit timeline.");
                }
                previousEnd = unit.EndSeconds;
            }
        }

        public bool IsResolved(int index)
        {
            return index >= 0 && index < results.Length && results[index] != null;
        }

        // A press belongs to the scheduled unit active when the button goes down.
        public int ActiveIndexAt(float seconds)
        {
            if (float.IsNaN(seconds) || seconds < 0f)
                return -1;

            for (int i = 0; i < units.Count; i++)
            {
                if (seconds >= units[i].StartSeconds &&
                    seconds < units[i].EndSeconds)
                    return i;
            }
            return -1;
        }

        // Resolve a captured press even if the next unit has appeared on screen.
        // Reject late releases rather than assigning them to the next unit.
        public bool TryRecordRelease(
            int index,
            float releasedAtSeconds,
            string pitchJudgment,
            float capturedPosition,
            float heldSeconds,
            float holdToleranceSeconds)
        {
            if (index < 0 || index >= units.Count || IsResolved(index) ||
                string.IsNullOrWhiteSpace(pitchJudgment) ||
                float.IsNaN(capturedPosition) ||
                float.IsInfinity(capturedPosition) ||
                capturedPosition < 0f || capturedPosition > 1f ||
                float.IsNaN(releasedAtSeconds) ||
                float.IsNaN(heldSeconds) || float.IsInfinity(heldSeconds) ||
                heldSeconds < 0f || float.IsNaN(holdToleranceSeconds) ||
                holdToleranceSeconds < 0f)
                return false;

            InputUnit unit = units[index];
            if (releasedAtSeconds < unit.StartSeconds ||
                releasedAtSeconds > unit.EndSeconds + ReleaseGraceSeconds)
                return false;

            bool? hold = unit.IsHold
                ? heldSeconds + holdToleranceSeconds >= unit.DurationSeconds
                : (bool?)null;

            Resolve(index, new TimedInputTrialResult
            {
                UnitId = unit.UnitId,
                PitchJudgment = pitchJudgment,
                CapturedPosition = capturedPosition,
                HeldSeconds = heldSeconds,
                HoldSucceeded = hold,
                TimedOut = false
            });
            return true;
        }

        // Production gameplay is input-driven, not tied to each note's
        // scheduled start/end. A player action advances one InputUnit.
        public bool TryRecordPlayerRelease(
            int index, string pitchJudgment, float capturedPosition,
            float heldSeconds, float holdToleranceSeconds)
        {
            if (index < 0 || index >= units.Count || IsResolved(index) ||
                string.IsNullOrWhiteSpace(pitchJudgment) ||
                float.IsNaN(capturedPosition) ||
                float.IsInfinity(capturedPosition) ||
                capturedPosition < 0f || capturedPosition > 1f ||
                float.IsNaN(heldSeconds) || float.IsInfinity(heldSeconds) ||
                heldSeconds < 0f ||
                float.IsNaN(holdToleranceSeconds) ||
                float.IsInfinity(holdToleranceSeconds) ||
                holdToleranceSeconds < 0f)
                return false;

            InputUnit unit = units[index];
            bool? hold = unit.IsHold
                ? heldSeconds + holdToleranceSeconds >= unit.DurationSeconds
                : (bool?)null;

            Resolve(index, new TimedInputTrialResult
            {
                UnitId = unit.UnitId,
                PitchJudgment = pitchJudgment,
                CapturedPosition = capturedPosition,
                HeldSeconds = heldSeconds,
                HoldSucceeded = hold,
                TimedOut = false
            });
            return true;
        }

        // Complete a HOLD exactly once when its required duration elapses.
        // The caller still gates advancement until the physical release.
        public bool TryRecordHoldSuccess(
            int index, string pitchJudgment, float capturedPosition, float heldSeconds)
        {
            if (index < 0 || index >= units.Count || IsResolved(index) ||
                !units[index].IsHold || string.IsNullOrWhiteSpace(pitchJudgment) ||
                float.IsNaN(capturedPosition) || float.IsInfinity(capturedPosition) ||
                capturedPosition < 0f || capturedPosition > 1f ||
                float.IsNaN(heldSeconds) || float.IsInfinity(heldSeconds) ||
                heldSeconds < 0f || heldSeconds + 0.0001f < units[index].RequiredHoldSeconds)
                return false;

            Resolve(index, new TimedInputTrialResult
            {
                UnitId = units[index].UnitId,
                PitchJudgment = pitchJudgment,
                CapturedPosition = capturedPosition,
                HeldSeconds = heldSeconds,
                HoldSucceeded = true,
                TimedOut = false
            });
            return true;
        }

        public void Expire(float elapsedSeconds)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (!IsResolved(i) &&
                    elapsedSeconds >= units[i].EndSeconds + ReleaseGraceSeconds)
                    ResolveMiss(i);
            }
        }

        private void ResolveMiss(int index)
        {
            Resolve(index, new TimedInputTrialResult
            {
                UnitId = units[index].UnitId,
                PitchJudgment = "Miss",
                HeldSeconds = 0f,
                HoldSucceeded = units[index].IsHold ? (bool?)false : null,
                TimedOut = true
            });
        }

        private void Resolve(int index, TimedInputTrialResult result)
        {
            if (IsResolved(index))
                return;

            results[index] = result;
            ResolvedCount++;
            OnResolved?.Invoke(result);
        }
    }
}
