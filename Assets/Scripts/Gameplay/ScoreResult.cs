
using System;
using System.Collections.Generic;

namespace UtaNoUtakeiko.Gameplay
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard
    }

    [Serializable]
    public class ScoreResult
    {
        public string SongId;
        public Difficulty Difficulty;

        public int Score;
        public int MaxScore;

        public int PerfectCount;
        public int GreatCount;
        public int GoodCount;
        public int BadCount;
        public int MissCount;

        public bool TimedOut;

        public List<UnitResult> UnitResults =
            new List<UnitResult>();

        public float AchievementRate =>
            MaxScore > 0
                ? (float)Score / MaxScore * 100f
                : 0f;
    }
}
