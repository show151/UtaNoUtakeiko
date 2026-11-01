
using System;

namespace UtaNoUtakeiko.Gameplay
{
    public enum InputState
    {
        Completed,
        TimedOut
    }

    public enum HoldJudgmentType
    {
        Perfect,
        Miss
    }

    [Serializable]
    public class UnitResult
    {
        public string UnitId;
        public InputState InputState;

        public int? CapturedPitchStep;
        public JudgmentType? Judgment;

        public int BaseScore;

        public HoldJudgmentType? HoldJudgment;
        public int HoldScore;
    }
}
