
using System.Collections.Generic;
using UtaNoUtakeiko.Data;

namespace UtaNoUtakeiko.Gameplay
{
    public static class RepeatMappingValidator
    {
        public static bool TryValidate(
            IList<RepeatMapping> mappings,
            out string error)
        {
            error = null;

            if (mappings == null)
            {
                error = "RepeatMapping一覧がnullです。";
                return false;
            }

            var targetIds = new HashSet<string>();
            var sourceIds = new HashSet<string>();

            foreach (var mapping in mappings)
            {
                if (mapping == null)
                {
                    error = "nullのRepeatMappingがあります。";
                    return false;
                }

                string context =
                    $"SongId={mapping.SongId}, " +
                    $"Source={mapping.SourceSectionId}, " +
                    $"Target={mapping.TargetSectionId}";

                if (string.IsNullOrWhiteSpace(mapping.SongId) ||
                    string.IsNullOrWhiteSpace(mapping.SourceSectionId) ||
                    string.IsNullOrWhiteSpace(mapping.TargetSectionId))
                {
                    error = $"必須IDが不正です: {context}";
                    return false;
                }

                if (mapping.SourceSectionId == mapping.TargetSectionId)
                {
                    error = $"自己参照は禁止です: {context}";
                    return false;
                }

                if (mapping.SourceStartMeasure < 1 ||
                    mapping.TargetStartMeasure < 1 ||
                    mapping.SourceEndMeasure < mapping.SourceStartMeasure ||
                    mapping.TargetEndMeasure < mapping.TargetStartMeasure)
                {
                    error = $"小節範囲が不正です: {context}";
                    return false;
                }

                if (mapping.SourceStartMeasure <= mapping.TargetEndMeasure &&
                    mapping.TargetStartMeasure <= mapping.SourceEndMeasure)
                {
                    error = $"元区間と繰り返し先が重複しています: {context}";
                    return false;
                }

                // 楽曲別に繰り返し先を管理
                string targetKey =
                    mapping.SongId + ":" + mapping.TargetSectionId;

                if (!targetIds.Add(targetKey))
                {
                    error = $"繰り返し先が重複しています: {context}";
                    return false;
                }

                sourceIds.Add(
                    mapping.SongId + ":" + mapping.SourceSectionId);
            }

            // 元区間が別Mappingの繰り返し先であれば入れ子
            foreach (var mapping in mappings)
            {
                string sourceKey =
                    mapping.SongId + ":" + mapping.SourceSectionId;

                if (targetIds.Contains(sourceKey))
                {
                    error =
                        $"入れ子・循環参照は禁止です: " +
                        $"SongId={mapping.SongId}, " +
                        $"SectionId={mapping.SourceSectionId}";
                    return false;
                }
            }

            return true;
        }
    }
}
