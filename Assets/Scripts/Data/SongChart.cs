
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
