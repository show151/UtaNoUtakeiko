
using System;

namespace UtaNoUtakeiko.Gameplay
{
    public enum JudgmentType
    {
        Perfect,
        Great,
        Good,
        Bad,
        Miss
    }

    [Serializable]
    public class NoteResult
    {
        public string NoteId;
        public JudgmentType Judgment;

        public NoteResult(string noteId, JudgmentType judgment)
        {
            NoteId = noteId;
            Judgment = judgment;
        }
    }
}
