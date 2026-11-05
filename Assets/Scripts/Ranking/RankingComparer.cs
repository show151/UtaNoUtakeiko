using System;
using System.Collections.Generic;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Ranking
{
    // A ranking contains results for exactly one song and difficulty.
    public sealed class RankingComparer : IComparer<ScoreResult>
    {
        public int Compare(ScoreResult x, ScoreResult y)
        {
            Validate(x);
            Validate(y);
            if (!string.Equals(x.SongId, y.SongId, StringComparison.Ordinal) || x.Difficulty != y.Difficulty)
                throw new ArgumentException("Cannot compare results from different rankings.");

            int result = y.Score.CompareTo(x.Score);
            if (result != 0) return result;
            result = y.PerfectCount.CompareTo(x.PerfectCount);
            if (result != 0) return result;
            result = y.GreatCount.CompareTo(x.GreatCount);
            if (result != 0) return result;
            result = y.GoodCount.CompareTo(x.GoodCount);
            if (result != 0) return result;
            result = x.BadCount.CompareTo(y.BadCount);
            return result != 0 ? result : x.MissCount.CompareTo(y.MissCount);
        }

        public static void Validate(ScoreResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (string.IsNullOrWhiteSpace(result.SongId) ||
                !Enum.IsDefined(typeof(Difficulty), result.Difficulty) ||
                result.MaxScore < 0 || result.Score < 0 || result.Score > result.MaxScore ||
                result.PerfectCount < 0 || result.GreatCount < 0 || result.GoodCount < 0 ||
                result.BadCount < 0 || result.MissCount < 0)
                throw new ArgumentException("Invalid ranking result.", nameof(result));
        }
    }

    public sealed class RankedResult
    {
        public int Rank { get; }
        public ScoreResult Result { get; }
        public RankedResult(int rank, ScoreResult result) { Rank = rank; Result = result; }
    }

    public static class RankingCalculator
    {
        // Filters before sorting, so results from another song or difficulty cannot leak in.
        public static List<RankedResult> Calculate(IEnumerable<ScoreResult> results, string songId, Difficulty difficulty)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (string.IsNullOrWhiteSpace(songId)) throw new ArgumentException("Song ID is required.", nameof(songId));
            if (!Enum.IsDefined(typeof(Difficulty), difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
            var matching = new List<ScoreResult>();
            foreach (var result in results)
            {
                RankingComparer.Validate(result);
                if (string.Equals(result.SongId, songId, StringComparison.Ordinal) && result.Difficulty == difficulty)
                    matching.Add(result);
            }
            var comparer = new RankingComparer();
            matching.Sort(comparer);
            var ranked = new List<RankedResult>();
            int rank = 0;
            for (int i = 0; i < matching.Count; i++)
            {
                if (i == 0 || comparer.Compare(matching[i - 1], matching[i]) != 0) rank = i + 1;
                ranked.Add(new RankedResult(rank, matching[i]));
            }
            return ranked;
        }
    }
}

