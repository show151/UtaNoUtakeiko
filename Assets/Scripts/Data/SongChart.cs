
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

        public List<Section> Sections = new List<Section>();

        // Lyrics shown in sync with the playback measure.
        public List<SongLyricLine> LyricLines = new List<SongLyricLine>();

        public List<RepeatMapping> RepeatMappings =
            new List<RepeatMapping>();
    }
}
