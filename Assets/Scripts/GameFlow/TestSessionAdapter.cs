
using UnityEngine;

namespace Uta.GameFlow
{
    /// <summary>
    /// Issue #21の正式データ接続前に使用する
    /// テスト専用セッションデータ。
    /// </summary>
    public class TestSessionAdapter : MonoBehaviour
    {
        public string SongId => "TEMP_SONG_001";
        public string DifficultyId => "TEMP_EASY";

        private readonly float[] testNotes =
        {
            0.25f,
            0.75f,
            0.50f,
            0.90f,
            0.10f
        };

        public bool IsTemporary => true;

        public float[] GetTestNotePositions()
        {
            return (float[])testNotes.Clone();
        }

        public bool Validate()
        {
            if (!SongId.StartsWith("TEMP_")
                || !DifficultyId.StartsWith("TEMP_"))
            {
                Debug.LogError(
                    "Test data must use TEMP_ identifiers."
                );
                return false;
            }

            if (testNotes.Length == 0)
            {
                Debug.LogError("Test note data is empty.");
                return false;
            }

            foreach (float position in testNotes)
            {
                if (float.IsNaN(position)
                    || float.IsInfinity(position)
                    || position < 0f
                    || position > 1f)
                {
                    Debug.LogError(
                        $"Invalid test note: {position}"
                    );
                    return false;
                }
            }

            return true;
        }

        public void StartTestSession()
        {
            if (!Validate())
            {
                Debug.LogError("Test session validation failed.");
                return;
            }

            Debug.Log(
                $"TEMP session prepared: {SongId} / {DifficultyId}"
            );
        }
    }
}
