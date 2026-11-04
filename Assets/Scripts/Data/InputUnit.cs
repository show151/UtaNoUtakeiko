using System;
using System.Collections.Generic;
using UnityEngine;

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
        // HOLD gameplay uses one real-time second per grouped source note.
        // Scheduled EndSeconds remains the compressed BPM preview timeline.
        public float RequiredHoldSeconds => IsHold
            ? (HoldDurationSeconds > 0f ? HoldDurationSeconds : Mathf.Max(1, SourceNoteIds == null ? 0 : SourceNoteIds.Count))
            : 0f;
        public float HoldDurationSeconds;
        public float DurationSeconds => EndSeconds - StartSeconds;
    }
}
