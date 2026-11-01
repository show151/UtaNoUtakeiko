
using System;
using System.Collections.Generic;

namespace UtaNoUtakeiko.Data
{
    [Serializable]
    public class SongChart
    {
        public string SongId;
        public string DifficultyId;

        public int Bpm;
        public int TimeSignatureNumerator;
        public int TimeSignatureDenominator;

        public List<Section> Sections = new List<Section>();

        public List<RepeatMapping> RepeatMappings =
            new List<RepeatMapping>();
    }
}
