using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace Uta.GameFlow
{
    // A read-only snapshot for rendering one confirmed result.
    public sealed class ResultDisplayModel
    {
        public readonly string SongName;
        public readonly int Score;
        public readonly float AchievementRate;
        public readonly string Rank;
        public readonly string JudgmentSummary;
        public readonly string Message;

        private ResultDisplayModel(
            string songName,
            int score,
            float achievementRate,
            string rank,
            string judgmentSummary,
            string message)
        {
            SongName = songName;
            Score = score;
            AchievementRate = achievementRate;
            Rank = rank;
            JudgmentSummary = judgmentSummary;
            Message = message;
        }

        public static ResultDisplayModel Create(SongChart chart, ScoreResult result)
        {
            string songName = !string.IsNullOrWhiteSpace(chart?.DisplayName)
                ? chart.DisplayName
                : !string.IsNullOrWhiteSpace(result?.SongId)
                    ? result.SongId
                    : "曲名不明";

            float rate = result != null ? result.AchievementRate : 0f;
            string rank = rate >= 95f ? "S"
                : rate >= 90f ? "A"
                : rate >= 80f ? "B"
                : rate >= 70f ? "C"
                : "D";

            string message = rate >= 95f ? "すごい！ほとんど音が合ってたよ。次はパーフェクトを目指そう♪"
                : rate >= 70f ? "一緒に歌えてうれしかったよ！次も楽しく歌おうね♪"
                : "最後までよく頑張ったね！次はもっと一緒に歌おう♪";

            string judgments = result != null
                ? $"Perfect  {result.PerfectCount}　 Great  {result.GreatCount}　 Good  {result.GoodCount}\nBad  {result.BadCount}　 Miss  {result.MissCount}"
                : "判定結果はありません";

            return new ResultDisplayModel(
                songName,
                result != null ? result.Score : 0,
                rate,
                rank,
                judgments,
                message);
        }
    }
}
