
using System;
using System.Collections.Generic;
using UtaNoUtakeiko.Data;

namespace UtaNoUtakeiko.Gameplay
{
    public class JudgmentSession
    {
        private readonly HashSet<string> allowedNoteIds =
            new HashSet<string>();

        private readonly Dictionary<string, NoteResult> results =
            new Dictionary<string, NoteResult>();

        private readonly List<string> orderedNoteIds =
            new List<string>();

        private readonly Dictionary<string, int?> capturedPitchSteps =
            new Dictionary<string, int?>();

        private bool isFinalized;

        public int TotalNotes => allowedNoteIds.Count;
        public int JudgedNotes => results.Count;
        public bool IsFinalized => isFinalized;

        public JudgmentSession(PlayPlan playPlan)
        {
            if (playPlan == null || playPlan.Notes == null)
                throw new ArgumentException("PlayPlanが不正です。");

            foreach (var note in playPlan.Notes)
            {
                if (note == null || note.IsRest ||
                    string.IsNullOrWhiteSpace(note.NoteId))
                {
                    throw new ArgumentException(
                        "入力対象の音符データが不正です。");
                }

                if (!allowedNoteIds.Add(note.NoteId))
                {
                    throw new ArgumentException(
                        $"NoteIdが重複しています: {note.NoteId}");
                }

                orderedNoteIds.Add(note.NoteId);
            }
        }

        public bool TryJudge(
            string noteId,
            int capturedPitchStep,
            JudgmentType judgment)
        {
            if (isFinalized ||
                string.IsNullOrWhiteSpace(noteId) ||
                !allowedNoteIds.Contains(noteId) ||
                results.ContainsKey(noteId) ||
                capturedPitchStep < 0 ||
                !Enum.IsDefined(typeof(JudgmentType), judgment))
            {
                return false;
            }

            results.Add(
                noteId,
                new NoteResult(noteId, judgment));

            capturedPitchSteps.Add(noteId, capturedPitchStep);

            return true;
        }

        public void FinalizeSession()
        {
            if (isFinalized)
                return;

            if (results.Count != allowedNoteIds.Count)
            {
                throw new InvalidOperationException(
                    "未入力の音符が残っています。通常終了できません。");
            }

            foreach (string noteId in orderedNoteIds)
            {
                if (!capturedPitchSteps.TryGetValue(
                        noteId, out int? pitch) ||
                    !pitch.HasValue)
                {
                    throw new InvalidOperationException(
                        $"入力音程がありません: {noteId}");
                }
            }

            isFinalized = true;
        }

        public IReadOnlyList<UnitResult> FinalizeTimedOut()
        {
            if (isFinalized)
            {
                throw new InvalidOperationException(
                    "すでに確定済みのセッションです。");
            }

            isFinalized = true;

            return GetUnitResults();
        }

        // 外部へ内部Dictionaryを直接渡さない
        public IReadOnlyDictionary<string, NoteResult> GetResults()
        {
            return new Dictionary<string, NoteResult>(results);
        }

        public IReadOnlyList<UnitResult> GetUnitResults()
        {
            if (!isFinalized)
            {
                throw new InvalidOperationException(
                    "セッションがまだ確定していません。");
            }

            var unitResults = new List<UnitResult>();

            foreach (string noteId in orderedNoteIds)
            {
                if (results.TryGetValue(noteId, out var noteResult))
                {
                    bool hasPitch = capturedPitchSteps.TryGetValue(
                        noteId, out int? pitch);

                    unitResults.Add(new UnitResult
                    {
                        UnitId = noteId,
                        InputState = InputState.Completed,
                        CapturedPitchStep = hasPitch ? pitch : null,
                        Judgment = noteResult.Judgment,
                        BaseScore = 0
                    });
                }
                else
                {
                    unitResults.Add(new UnitResult
                    {
                        UnitId = noteId,
                        InputState = InputState.TimedOut,
                        CapturedPitchStep = null,
                        Judgment = null,
                        BaseScore = 0,
                        HoldJudgment = null,
                        HoldScore = 0
                    });
                }
            }

            return unitResults.AsReadOnly();
        }
    }
}
