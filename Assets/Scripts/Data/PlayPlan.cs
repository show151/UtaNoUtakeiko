using System;
using System.Collections.Generic;

namespace UtaNoUtakeiko.Data
{
    [Serializable]
    public class PlayPlan
    {
        public string SongId;

        // ÀÛ‚É“ü—Í‚·‚é‹æŠÔ‚Ì‡˜
        public List<string> SectionIds = new List<string>();

        // “ü—Í‘ÎÛ‚Æ‚È‚é‰¹•„i‹x•„EŒJ‚è•Ô‚µÈ—ª‹æŠÔ‚ğœ‚­j
        public List<Note> Notes = new List<Note>();
    }
}