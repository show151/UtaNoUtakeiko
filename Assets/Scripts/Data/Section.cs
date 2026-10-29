
using System;
using System.Collections.Generic;

namespace UtaNoUtakeiko.Data
{
    [Serializable]
    public class Section
    {
        // 楽曲ID
        public string SongId;

        // 区間ID（例：SECTION_A）
        public string SectionId;

        // 開始小節（1から開始）
        public int StartMeasure;

        // 終了小節（両端を含む）
        public int EndMeasure;

        // 区間内の音符一覧
        public List<Note> Notes = new List<Note>();
    }
}
