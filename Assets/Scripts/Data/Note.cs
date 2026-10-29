
using System;

namespace UtaNoUtakeiko.Data
{
    [Serializable]
    public class Note
    {
        // 音符の識別子
        public string NoteId;

        // 小節番号（1から開始）
        public int MeasureNumber;

        // 小節内の音符順序（0から開始）
        public int NoteIndexInMeasure;

        // 目標音程（1メモリ＝1半音）
        public int TargetPitchStep;

        // 音符長（拍単位）
        public float DurationBeats;

        // 休符かどうか
        public bool IsRest;

        // 長押し対象かどうか
        public bool IsHold;
    }
}
