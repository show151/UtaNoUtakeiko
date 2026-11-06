using System;
using System.Collections.Generic;

namespace UtaNoUtakeiko.Data
{
    // A player action, not an original score note. Never mutate source Notes.
    [Serializable]
    public class InputUnit
    {
        public string UnitId;
        public int TargetPitchStep;
        public bool IsHold;
        public float StartSeconds;
        public float EndSeconds;
        public List<string> SourceNoteIds = new List<string>();
        // Production HOLD defaults to one second per input, regardless of note count.
        // Scheduled EndSeconds remains the compressed BPM preview timeline.
        public float RequiredHoldSeconds => IsHold
            ? (HoldDurationSeconds > 0f ? HoldDurationSeconds : 1f)
            : 0f;
        public float HoldDurationSeconds;
        public float DurationSeconds => EndSeconds - StartSeconds;
    }
}
