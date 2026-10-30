using System;
using System.Collections.Generic;

namespace UtaNoUtakeiko.Data
{
    [Serializable]
    public class PlaybackSection
    {
        // 実際に再生する区間ID
        public string SectionId;

        // 入力結果を参照する元区間ID
        public string SourceSectionId;

        // 繰り返しによって入力を再利用するか
        public bool IsRepeated;

        // 再生順に並べた音符
        public List<Note> Notes = new List<Note>();

        // Notesと同じ順番で対応する入力元NoteId
        // 休符の場合はnullとする
        public List<string> SourceNoteIds = new List<string>();
    }

    [Serializable]
    public class PlaybackPlan
    {
        public string SongId;

        // 歌唱再生する全区間（繰り返しを含む）
        public List<PlaybackSection> Sections =
            new List<PlaybackSection>();
    }
}