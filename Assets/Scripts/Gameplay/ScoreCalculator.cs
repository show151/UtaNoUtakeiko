
using System;
using System.Collections.Generic;
using UtaNoUtakeiko.Data;

namespace UtaNoUtakeiko.Gameplay
{
    public static class ScoreCalculator
    {
        public static ScoreResult Calculate(
            PlayPlan playPlan,
            IReadOnlyList<UnitResult> unitResults,
            ScorePolicy policy,
            Difficulty difficulty,
            bool timedOut)
        {
            if (playPlan == null || playPlan.Notes == null ||
                unitResults == null || policy == null)
            {
                throw new ArgumentException("採点データが不正です。");
            }

            var notes = playPlan.Notes;

            var noteIds = new HashSet<string>();

            foreach (var note in notes)
            {
                if (note == null ||
                    note.IsRest ||
                    string.IsNullOrWhiteSpace(note.NoteId))
                {
                    throw new ArgumentException(
                        "PlayPlanに不正な入力対象音符があります。");
                }

                if (!noteIds.Add(note.NoteId))
                {
                    throw new ArgumentException(
                        $"PlayPlan内でNoteIdが重複しています: {note.NoteId}");
                }
            }

            var resultMap = new Dictionary<string, UnitResult>();

            foreach (var unit in unitResults)
            {
                if (unit == null ||
                    string.IsNullOrWhiteSpace(unit.UnitId) ||
                    !resultMap.TryAdd(unit.UnitId, unit))
                {
                    throw new ArgumentException(
                        "UnitResultが不正または重複しています。");
                }
            }

            if (notes.Count != resultMap.Count)
            {
                throw new ArgumentException(
                    "採点対象と結果の件数が一致しません。");
            }

            long earnedWeight = 0;
            long maximumWeight = 0;

            var scoreResult = new ScoreResult
            {
                SongId = playPlan.SongId,
                Difficulty = difficulty,
                TimedOut = timedOut
            };

            foreach (var note in notes)
            {
                if (!resultMap.TryGetValue(note.NoteId, out var unit))
                {
                    throw new ArgumentException(
                        $"結果が存在しません: {note.NoteId}");
                }

                var scoredUnit = new UnitResult
                {
                    UnitId = unit.UnitId,
                    InputState = unit.InputState,
                    CapturedPitchStep = unit.CapturedPitchStep,
                    Judgment = unit.Judgment,
                    BaseScore = 0,
                    HoldJudgment = unit.HoldJudgment,
                    HoldScore = 0
                };

                bool isHold = note.IsHold;

                // 通常判定の最大重み
                maximumWeight += policy.PerfectWeight;

                // 長押しは保持判定の最大重みも加える
                if (isHold)
                    maximumWeight += policy.HoldPerfectWeight;

                if (unit.InputState == InputState.TimedOut)
                {
                    if (!timedOut ||
                        unit.CapturedPitchStep.HasValue ||
                        unit.Judgment.HasValue ||
                        unit.BaseScore != 0 ||
                        unit.HoldJudgment.HasValue ||
                        unit.HoldScore != 0)
                    {
                        throw new ArgumentException(
                            $"時間切れ結果が不正です: {unit.UnitId}");
                    }
                }
                else if (unit.InputState == InputState.Completed)
                {
                    if (!unit.CapturedPitchStep.HasValue ||
                        !unit.Judgment.HasValue)
                    {
                        throw new ArgumentException(
                            $"入力結果が不完全です: {unit.UnitId}");
                    }

                    int baseWeight = policy.GetWeight(unit.Judgment.Value);

                    earnedWeight += baseWeight;
                    scoredUnit.BaseScore = baseWeight;

                    AddJudgmentCount(
                        scoreResult, unit.Judgment.Value);

                    if (isHold)
                    {
                        if (!unit.HoldJudgment.HasValue)
                        {
                            throw new ArgumentException(
                                $"保持判定がありません: {unit.UnitId}");
                        }

                        int holdWeight = policy.GetHoldWeight(
                            unit.HoldJudgment.Value);

                        earnedWeight += holdWeight;
                        scoredUnit.HoldScore = holdWeight;

                        AddJudgmentCount(
                            scoreResult,
                            unit.HoldJudgment.Value ==
                                HoldJudgmentType.Perfect
                                    ? JudgmentType.Perfect
                                    : JudgmentType.Miss);
                    }
                    else if (unit.HoldJudgment.HasValue)
                    {
                        throw new ArgumentException(
                            $"通常音符に保持判定があります: {unit.UnitId}");
                    }
                }
                else
                {
                    throw new ArgumentException(
                        $"入力状態が不正です: {unit.UnitId}");
                }

                scoreResult.UnitResults.Add(scoredUnit);
            }

            scoreResult.MaxScore = ScorePolicy.DefaultMaxScore;

            if (maximumWeight > 0)
            {
                long cumulativeWeight = 0;
                int previousScore = 0;

                foreach (var unit in scoreResult.UnitResults)
                {
                    int baseWeight = unit.BaseScore;
                    int holdWeight = unit.HoldScore;

                    cumulativeWeight += baseWeight;

                    int baseTotal = (int)Math.Round(
                        (double)cumulativeWeight /
                        maximumWeight *
                        scoreResult.MaxScore,
                        MidpointRounding.AwayFromZero);

                    unit.BaseScore = baseTotal - previousScore;
                    previousScore = baseTotal;

                    cumulativeWeight += holdWeight;

                    int holdTotal = (int)Math.Round(
                        (double)cumulativeWeight /
                        maximumWeight *
                        scoreResult.MaxScore,
                        MidpointRounding.AwayFromZero);

                    unit.HoldScore = holdTotal - previousScore;
                    previousScore = holdTotal;
                }

                scoreResult.Score = previousScore;
            }
            else
            {
                scoreResult.Score = 0;
            }

            return scoreResult;
        }

        private static void AddJudgmentCount(
            ScoreResult result,
            JudgmentType judgment)
        {
            switch (judgment)
            {
                case JudgmentType.Perfect:
                    result.PerfectCount++;
                    break;
                case JudgmentType.Great:
                    result.GreatCount++;
                    break;
                case JudgmentType.Good:
                    result.GoodCount++;
                    break;
                case JudgmentType.Bad:
                    result.BadCount++;
                    break;
                case JudgmentType.Miss:
                    result.MissCount++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(judgment));
            }
        }
    }
}
