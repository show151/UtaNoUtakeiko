using System;
using System.Collections.Generic;
using System.Linq;
using UtaNoUtakeiko.Data;

namespace UtaNoUtakeiko.Gameplay
{
    public static class PlanBuilder
    {
        public static bool TryBuild(
            IList<Section> sections,
            IList<RepeatMapping> mappings,
            out PlayPlan playPlan,
            out PlaybackPlan playbackPlan,
            out string error)
        {
            playPlan = null;
            playbackPlan = null;
            error = null;

            if (sections == null || sections.Count == 0)
            {
                error = "区間データがありません。";
                return false;
            }

            if (!RepeatMappingValidator.TryValidate(
                mappings, out error))
                return false;

            if (sections.Any(s => s == null ||
                                  string.IsNullOrWhiteSpace(s.SongId) ||
                                  string.IsNullOrWhiteSpace(s.SectionId) ||
                                  s.Notes == null))
            {
                error = "不正な区間データがあります。";
                return false;
            }

            string songId = sections[0].SongId;

            if (sections.Any(s => s.SongId != songId))
            {
                error = "異なる楽曲の区間が混在しています。";
                return false;
            }

            var ordered = sections
                .OrderBy(s => s.StartMeasure)
                .ToList();

            // 区間の小節範囲が重複していないか検証
            for (int i = 0; i < ordered.Count; i++)
            {
                Section current = ordered[i];

                if (current.StartMeasure < 1 ||
                    current.EndMeasure < current.StartMeasure)
                {
                    error = $"小節範囲が不正です: SectionId={current.SectionId}";
                    return false;
                }

                if (i > 0)
                {
                    Section previous = ordered[i - 1];

                    // 終了小節と開始小節は両端を含むので、等しい場合も重複
                    if (current.StartMeasure <= previous.EndMeasure)
                    {
                        error =
                            $"区間の小節範囲が重複しています: " +
                            $"{previous.SectionId} / {current.SectionId}";
                        return false;
                    }
                }
            }

            // 楽曲全体でNoteIdが一意であることを確認
            var allNoteIds = new HashSet<string>();

            foreach (var section in ordered)
            {
                foreach (var note in section.Notes)
                {
                    if (note == null)
                    {
                        error = $"nullの音符があります: SectionId={section.SectionId}";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(note.NoteId))
                    {
                        error = $"音符IDが空です: SectionId={section.SectionId}";
                        return false;
                    }

                    if (!allNoteIds.Add(note.NoteId))
                    {
                        error =
                            $"楽曲内でNoteIdが重複しています: " +
                            $"NoteId={note.NoteId}, SectionId={section.SectionId}";
                        return false;
                    }
                }
            }

            var byId = new Dictionary<string, Section>();

            foreach (var section in ordered)
            {
                if (byId.ContainsKey(section.SectionId))
                {
                    error = $"区間IDが重複しています: {section.SectionId}";
                    return false;
                }

                byId.Add(section.SectionId, section);
            }

            // 繰り返しの対応が実際の譜面と一致するか検証
            var validMappings = new Dictionary<string, RepeatMapping>();

            foreach (var mapping in mappings)
            {
                if (!byId.TryGetValue(mapping.SourceSectionId,
                                     out var source) ||
                    !byId.TryGetValue(mapping.TargetSectionId,
                                     out var target))
                {
                    error = "存在しない区間を参照しています。";
                    return false;
                }

                if (!RepeatMatcher.TryMatch(
                    source, target, out _, out string matchError))
                {
                    // 不一致は省略せず、通常の入力対象に戻す
                    continue;
                }

                if (mapping.SongId != songId ||
                    mapping.SourceStartMeasure != source.StartMeasure ||
                    mapping.SourceEndMeasure != source.EndMeasure ||
                    mapping.TargetStartMeasure != target.StartMeasure ||
                    mapping.TargetEndMeasure != target.EndMeasure)
                {
                    error = "RepeatMappingと区間情報が一致しません。";
                    return false;
                }

                validMappings.Add(target.SectionId, mapping);
            }

            var input = new PlayPlan { SongId = songId };
            var output = new PlaybackPlan { SongId = songId };

            foreach (var section in ordered)
            {
                bool repeated = validMappings.TryGetValue(
                    section.SectionId, out var mapping);

                Section sourceSection = repeated
                    ? byId[mapping.SourceSectionId]
                    : section;

                if (!repeated)
                {
                    input.SectionIds.Add(section.SectionId);
                    input.Notes.AddRange(GetPlayableNotes(section));
                }

                var playbackSection = new PlaybackSection
                {
                    SectionId = section.SectionId,
                    SourceSectionId = sourceSection.SectionId,
                    IsRepeated = repeated
                };

                var targetNotes = GetOrderedNotes(section);
                var sourcePlayable = GetPlayableNotes(sourceSection);
                int sourceIndex = 0;

                foreach (var note in targetNotes)
                {
                    playbackSection.Notes.Add(note);

                    if (note.IsRest)
                    {
                        playbackSection.SourceNoteIds.Add(null);
                    }
                    else
                    {
                        string sourceNoteId;

                        if (repeated)
                        {
                            sourceNoteId = sourcePlayable[sourceIndex].NoteId;
                            sourceIndex++;
                        }
                        else
                        {
                            sourceNoteId = note.NoteId;
                        }

                        playbackSection.SourceNoteIds.Add(sourceNoteId);
                    }
                }

                output.Sections.Add(playbackSection);
            }

            playPlan = input;
            playbackPlan = output;
            return true;
        }

        private static List<Note> GetOrderedNotes(Section section)
        {
            return section.Notes
                .OrderBy(n => n.MeasureNumber)
                .ThenBy(n => n.NoteIndexInMeasure)
                .ToList();
        }

        private static List<Note> GetPlayableNotes(Section section)
        {
            return GetOrderedNotes(section)
                .Where(n => !n.IsRest)
                .ToList();
        }
    }
}