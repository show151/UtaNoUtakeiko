using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UtaNoUtakeiko.Gameplay;
using UtaNoUtakeiko.Ranking;

namespace UtaNoUtakeiko.Tests
{
    public class RankingRepositoryTests
    {
        private string directory;
        private string path;
        [SetUp] public void SetUp() { directory = Path.Combine(Path.GetTempPath(), "uta-ranking-" + Guid.NewGuid().ToString("N")); path = Path.Combine(directory, "ranking.json"); }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        private static RankingEntry Entry(string id, string song, Difficulty difficulty, int score)
        {
            return new RankingEntry { Id = id, Name = "名前", Result = new ScoreResult { SongId = song, Difficulty = difficulty, Score = score, MaxScore = 1000 } };
        }
        [Test] public void SaveAndReload_PreservesRecordsAndKeys()
        {
            var repo = new RankingRepository(path);
            Assert.IsEmpty(repo.Load());
            repo.Save(new List<RankingEntry> { Entry("1", "a", Difficulty.Easy, 900), Entry("2", "b", Difficulty.Hard, 800) });
            var loaded = new RankingRepository(path).Load();
            Assert.AreEqual(2, loaded.Count);
            Assert.AreEqual("名前", loaded[0].Name);
            Assert.AreEqual(900, loaded[0].Result.Score);
            Assert.AreEqual("b", loaded[1].Result.SongId);
            Assert.AreEqual(Difficulty.Hard, loaded[1].Result.Difficulty);
            repo.Save(loaded);
            Assert.AreEqual(2, repo.Load().Count);
        }
        [Test] public void CorruptedData_DoesNotStopPlay_AndIsBackedUpWhenSaving()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "invalid json");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("ランキングデータを読み込めませんでした"));
            var repo = new RankingRepository(path);
            Assert.IsEmpty(repo.Load());
            repo.Save(new List<RankingEntry> { Entry("1", "a", Difficulty.Easy, 500) });
            Assert.AreEqual("invalid json", File.ReadAllText(path + ".bak"));
            Assert.AreEqual(1, new RankingRepository(path).Load().Count);
        }
    }
}
