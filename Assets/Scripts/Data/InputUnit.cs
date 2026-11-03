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
        public float DurationSeconds => EndSeconds - StartSeconds;
    }
}
