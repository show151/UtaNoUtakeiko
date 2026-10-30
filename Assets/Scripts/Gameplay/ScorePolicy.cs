
using System;

namespace UtaNoUtakeiko.Gameplay
{
    [Serializable]
    public class ScorePolicy
    {
        public const int DefaultMaxScore = 1000000;

        public int PerfectWeight;
        public int GreatWeight;
        public int GoodWeight;
        public int BadWeight;
        public int MissWeight;

        public int HoldPerfectWeight;
        public int HoldMissWeight;

        public ScorePolicy(
            int perfect,
            int great,
            int good,
            int bad,
            int miss,
            int holdPerfect,
            int holdMiss)
        {
            if (perfect <= 0 ||
                great < 0 || good < 0 ||
                bad < 0 || miss < 0 ||
                holdPerfect < 0 || holdMiss < 0)
            {
                throw new ArgumentOutOfRangeException(
                    "配点には有効な非負値を指定してください。");
            }

            if (!(perfect >= great &&
                  great >= good &&
                  good >= bad &&
                  bad >= miss))
            {
                throw new ArgumentException(
                    "通常判定の配点順序が不正です。");
            }

            if (holdPerfect < holdMiss)
            {
                throw new ArgumentException(
                    "保持判定の配点順序が不正です。");
            }

            PerfectWeight = perfect;
            GreatWeight = great;
            GoodWeight = good;
            BadWeight = bad;
            MissWeight = miss;

            HoldPerfectWeight = holdPerfect;
            HoldMissWeight = holdMiss;
        }

        public int GetWeight(JudgmentType judgment)
        {
            switch (judgment)
            {
                case JudgmentType.Perfect: return PerfectWeight;
                case JudgmentType.Great: return GreatWeight;
                case JudgmentType.Good: return GoodWeight;
                case JudgmentType.Bad: return BadWeight;
                case JudgmentType.Miss: return MissWeight;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(judgment));
            }
        }

        public int GetHoldWeight(HoldJudgmentType judgment)
        {
            return judgment == HoldJudgmentType.Perfect
                ? HoldPerfectWeight
                : HoldMissWeight;
        }
    }
}
