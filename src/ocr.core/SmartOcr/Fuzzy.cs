using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ocr.core.SmartOcr
{
    public static class Fuzzy
    {
        public static int LevenshteinDistance(string a, string b)
        {
            if (a == b) return 0;
            if (string.IsNullOrEmpty(a)) return b.Length;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var d = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++)
                d[i, 0] = i;

            for (int j = 0; j <= b.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1,     // delete
                                 d[i, j - 1] + 1),    // insert
                        d[i - 1, j - 1] + cost        // substitute
                    );
                }
            }

            return d[a.Length, b.Length];
        }

        public static string? BestMatch(string input, IEnumerable<string> candidates, int maxDistance = 2)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            string? best = null;
            int bestDist = int.MaxValue;

            foreach (var candidate in candidates)
            {
                var d = LevenshteinDistance(input, candidate);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = candidate;
                }
            }

            return bestDist <= maxDistance ? best : null;
        }
    }
}
