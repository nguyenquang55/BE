using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Application.Service.Helpers
{
    internal static class TitleKeyBuilder
    {
        public static string Normalize(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            var trimmed = title.Trim().ToLowerInvariant();
            var normalized = trimmed.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    if (!char.IsControl(c))
                    {
                        builder.Append(c);
                    }
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        public static string Build(Guid userId, string bucket, string title, DateTimeOffset? start, DateTimeOffset? end)
        {
            var normalized = Normalize(title);
            var payload = string.Join('|', userId.ToString("N"), bucket, normalized, Format(start), Format(end));
            using var sha = SHA256.Create();
            var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
            return $"{bucket}:{userId:N}:{hash}";
        }

        public static bool MatchesWindow(DateTimeOffset? requestStart, DateTimeOffset? requestEnd, DateTimeOffset? candidateStart, DateTimeOffset? candidateEnd)
        {
            if (requestStart == null && requestEnd == null)
                return true;

            var reqStart = requestStart ?? candidateStart;
            var reqEnd = requestEnd ?? candidateEnd;
            if (reqStart == null || reqEnd == null)
                return true;

            var candStart = candidateStart ?? DateTimeOffset.MinValue;
            var candEnd = candidateEnd ?? DateTimeOffset.MaxValue;
            return candStart <= reqEnd && candEnd >= reqStart;
        }

        private static string Format(DateTimeOffset? value)
        {
            return value?.ToUniversalTime().ToString("yyyyMMddHHmmss") ?? "na";
        }
    }
}
