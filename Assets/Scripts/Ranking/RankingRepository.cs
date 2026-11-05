using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Ranking
{
    [Serializable]
    public sealed class RankingEntry
    {
        public string Id;
        public string Name;
        public ScoreResult Result;
    }

    [Serializable]
    internal sealed class RankingFile
    {
        public int Version = 1;
        public List<RankingEntry> Entries = new List<RankingEntry>();
    }

    public sealed class RankingRepository
    {
        private readonly string path;
        public RankingRepository(string path) { this.path = path; }

        public List<RankingEntry> Load()
        {
            if (!File.Exists(path)) return new List<RankingEntry>();
            try
            {
                var file = JsonUtility.FromJson<RankingFile>(File.ReadAllText(path));
                if (file == null || file.Version != 1 || file.Entries == null) throw new FormatException("Invalid ranking file");
                var ids = new HashSet<string>();
                foreach (var entry in file.Entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id) || entry.Name == null)
                        throw new FormatException("Invalid ranking entry");
                    RankingComparer.Validate(entry.Result);
                }
                return file.Entries;
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("ランキングデータを読み込めませんでした: " + e.Message);
                return new List<RankingEntry>();
            }
        }

        public void Save(List<RankingEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            foreach (var entry in entries) RankingComparer.Validate(entry.Result);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(new RankingFile { Entries = entries }));
            if (File.Exists(path))
            {
                string backup = path + ".bak";
                File.Replace(temporary, path, backup);
            }
            else File.Move(temporary, path);
        }
    }
}
