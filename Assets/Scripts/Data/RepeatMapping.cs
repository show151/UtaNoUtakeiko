
using System;

namespace UtaNoUtakeiko.Data
{
    [Serializable]
    public class RepeatMapping
    {
        // 楽曲ID
        public string SongId;

        // 元区間のID（入力・採点する区間）
        public string SourceSectionId;

        // 繰り返し先の区間ID（入力を省略する区間）
        public string TargetSectionId;

        // 元区間の開始・終了小節
        public int SourceStartMeasure;
        public int SourceEndMeasure;

        // 繰り返し先の開始・終了小節
        public int TargetStartMeasure;
        public int TargetEndMeasure;
    }
}
