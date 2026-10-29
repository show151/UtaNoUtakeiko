
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
                error = "RepeatMappingàÍóóÇ™nullÇ≈Ç∑ÅB";
                return false;
            }

            var targetIds = new HashSet<string>();
            var sourceIds = new HashSet<string>();

            foreach (var mapping in mappings)
            {
                if (mapping == null)
                {
                    error = "nullÇÃRepeatMappingÇ™Ç†ÇËÇ‹Ç∑ÅB";
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
                    error = $"ïKê{IDÇ™ïsê≥Ç≈Ç∑: {context}";
                    return false;
                }

                if (mapping.SourceSectionId == mapping.TargetSectionId)
                {
                    error = $"é©å»éQè∆ÇÕã÷é~Ç≈Ç∑: {context}";
                    return false;
                }

                if (mapping.SourceStartMeasure < 1 ||
                    mapping.TargetStartMeasure < 1 ||
                    mapping.SourceEndMeasure < mapping.SourceStartMeasure ||
                    mapping.TargetEndMeasure < mapping.TargetStartMeasure)
                {
                    error = $"è¨êﬂîÕàÕÇ™ïsê≥Ç≈Ç∑: {context}";
                    return false;
                }

                if (mapping.SourceStartMeasure <= mapping.TargetEndMeasure &&
                    mapping.TargetStartMeasure <= mapping.SourceEndMeasure)
                {
                    error = $"å≥ãÊä‘Ç∆åJÇËï‘ÇµêÊÇ™èdï°ÇµÇƒÇ¢Ç‹Ç∑: {context}";
                    return false;
                }

                // äyã»ï Ç…åJÇËï‘ÇµêÊÇä«óù
                string targetKey =
                    mapping.SongId + ":" + mapping.TargetSectionId;

                if (!targetIds.Add(targetKey))
                {
                    error = $"åJÇËï‘ÇµêÊÇ™èdï°ÇµÇƒÇ¢Ç‹Ç∑: {context}";
                    return false;
                }

                sourceIds.Add(
                    mapping.SongId + ":" + mapping.SourceSectionId);
            }

            // å≥ãÊä‘Ç™ï MappingÇÃåJÇËï‘ÇµêÊÇ≈Ç†ÇÍÇŒì¸ÇÍéq
            foreach (var mapping in mappings)
            {
                string sourceKey =
                    mapping.SongId + ":" + mapping.SourceSectionId;

                if (targetIds.Contains(sourceKey))
                {
                    error =
                        $"ì¸ÇÍéqÅEèzä¬éQè∆ÇÕã÷é~Ç≈Ç∑: " +
                        $"SongId={mapping.SongId}, " +
                        $"SectionId={mapping.SourceSectionId}";
                    return false;
                }
            }

            return true;
        }
    }
}
