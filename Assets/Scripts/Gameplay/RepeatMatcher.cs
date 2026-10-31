
using System;
using System.Collections.Generic;
using System.Linq;
using UtaNoUtakeiko.Data;

namespace UtaNoUtakeiko.Gameplay
{
    public static class RepeatMatcher
    {
        public static bool TryMatch(
            Section source,
            Section target,
            out RepeatMapping mapping,
            out string error)
        {
            mapping = null;
            error = null;

            if (source == null || target == null)
            {
                error = "元区間または対象区間がnullです。";
                return false;
            }

            string context =
                $"SongId={source.SongId}, " +
                $"Source={source.SectionId}, " +
                $"Target={target.SectionId}";

            // 1. 基本データの確認
            if (string.IsNullOrWhiteSpace(source.SongId) ||
                string.IsNullOrWhiteSpace(source.SectionId) ||
                string.IsNullOrWhiteSpace(target.SongId) ||
                string.IsNullOrWhiteSpace(target.SectionId) ||
                source.Notes == null ||
                target.Notes == null)
            {
                error = $"不正な区間データ: {context}";
                return false;
            }

            // 2. 楽曲IDの一致
            if (source.SongId != target.SongId)
            {
                error = $"楽曲IDが一致しません: {context}";
                return false;
            }

            // 3. 小節範囲の確認
            if (source.StartMeasure < 1 ||
                target.StartMeasure < 1 ||
                source.EndMeasure < source.StartMeasure ||
                target.EndMeasure < target.StartMeasure)
            {
                error = $"小節範囲が不正です: {context}";
                return false;
            }

            // 元区間と繰り返し先の重複を拒否
            if (source.StartMeasure <= target.EndMeasure &&
                target.StartMeasure <= source.EndMeasure)
            {
                error = $"小節範囲が重複しています: {context}";
                return false;
            }

            // 4. 音符データを検証
            if (!ValidateNotes(source, out error) ||
                !ValidateNotes(target, out error))
            {
                return false;
            }

            // 5. 休符を除いて小節順・音符順に整列
            List<Note> sourceNotes = GetPlayableNotes(source);
            List<Note> targetNotes = GetPlayableNotes(target);

            // 6. 音符数を比較
            if (sourceNotes.Count != targetNotes.Count)
            {
                error = $"音符数が一致しません: {context}";
                return false;
            }

            // 7. 音程を完全一致で比較
            for (int i = 0; i < sourceNotes.Count; i++)
            {
                if (sourceNotes[i].TargetPitchStep !=
                    targetNotes[i].TargetPitchStep)
                {
                    error =
                        $"音程が一致しません: {context}, NoteIndex={i}";
                    return false;
                }
            }

            // 8. 一致時のみ対応関係を生成
            mapping = new RepeatMapping
            {
                SongId = source.SongId,
                SourceSectionId = source.SectionId,
                TargetSectionId = target.SectionId,
                SourceStartMeasure = source.StartMeasure,
                SourceEndMeasure = source.EndMeasure,
                TargetStartMeasure = target.StartMeasure,
                TargetEndMeasure = target.EndMeasure
            };

            return true;
        }

        private static List<Note> GetPlayableNotes(Section section)
        {
            return section.Notes
                .Where(note => !note.IsRest)
                .OrderBy(note => note.MeasureNumber)
                .ThenBy(note => note.NoteIndexInMeasure)
                .ToList();
        }

        private static bool ValidateNotes(
            Section section,
            out string error)
        {
            error = null;

            var positions = new HashSet<string>();
            var ids = new HashSet<string>();

            foreach (Note note in section.Notes)
            {
                if (note == null)
                {
                    error =
                        $"nullの音符があります: SongId={section.SongId}, SectionId={section.SectionId}";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(note.NoteId) ||
                    !ids.Add(note.NoteId))
                {
                    error =
                        $"音符IDが不正または重複しています: SongId={section.SongId}, SectionId={section.SectionId}";
                    return false;
                }

                if (note.MeasureNumber < section.StartMeasure ||
                    note.MeasureNumber > section.EndMeasure ||
                    note.NoteIndexInMeasure < 0 ||
                    float.IsNaN(note.DurationBeats) ||
                    float.IsInfinity(note.DurationBeats) ||
                    note.DurationBeats <= 0 ||
                    (!note.IsRest && note.TargetPitchStep < 0))
                {
                    error =
                        $"音符データが不正です: SongId={section.SongId}, SectionId={section.SectionId}, NoteId={note.NoteId}";
                    return false;
                }

                string position =
                    $"{note.MeasureNumber}:{note.NoteIndexInMeasure}";

                if (!positions.Add(position))
                {
                    error =
                        $"音符位置が重複しています: SongId={section.SongId}, SectionId={section.SectionId}, Position={position}";
                    return false;
                }
            }

            return true;
        }
    }
}
