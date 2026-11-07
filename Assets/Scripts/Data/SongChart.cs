
using System;
using System.Collections.Generic;

namespace UtaNoUtakeiko.Data
{
    [Serializable]
    public class SongLyricLine
    {
        public int StartMeasure;
        public int EndMeasure;
        public string Text;
        // Optional note-level mapping for lyrics that begin within a measure.
        public List<string> NoteIds = new List<string>();
        public List<string> Syllables = new List<string>();

        public string FormatForNote(string noteId)
        {
            if (string.IsNullOrEmpty(noteId) || NoteIds == null || Syllables == null ||
                NoteIds.Count != Syllables.Count)
                return null;
            int current = NoteIds.IndexOf(noteId);
            if (current < 0) return null;
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < Syllables.Count; i++)
            {
                if (i == current) result.Append("<color=#FFD45C>");
                result.Append(Syllables[i]);
                if (i == current) result.Append("</color>");
            }
            return result.ToString();
        }
    }

    [Serializable]
    public class SongChart
    {
        public string SongId;
        public string DisplayName;
        public string DifficultyId;

        public int Bpm;
        public int TimeSignatureNumerator;
        public int TimeSignatureDenominator;

        // Zero retains the legacy C4-to-A4 display range for older charts.
        public int PitchBaseMidi;
        public int PitchStepCount;
        // Zero means the first bar is full; positive values describe a pickup.
        public float PickupBeats;

        public List<Section> Sections = new List<Section>();

        // Lyrics shown in sync with the playback measure.
        public List<SongLyricLine> LyricLines = new List<SongLyricLine>();

        public List<RepeatMapping> RepeatMappings =
            new List<RepeatMapping>();
    }
}
