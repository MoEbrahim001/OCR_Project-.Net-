using System;
using System.Collections.Generic;
using System.Linq;

namespace ocr.core.SmartOcr
{
    public class SmartCorrectionService
    {
        private string CorrectFromLexicon(string? raw, string[] lexicon, int maxDistance = 2)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var norm = ArabicTextNormalizer.Normalize(raw);

            // 1) جرب تطابق exact بعد الـ normalize
            var exact = lexicon.FirstOrDefault(item =>
                ArabicTextNormalizer.Normalize(item) == norm);

            if (exact != null)
                return exact;

            // 2) جرب fuzzy
            var normLexicon = lexicon
                .Select(ArabicTextNormalizer.Normalize)
                .ToArray();

            var bestNorm = Fuzzy.BestMatch(norm, normLexicon, maxDistance);
            if (bestNorm == null)
                return raw;   // سيبه زي ما هو لو مش عارف يصححه

            // رجّع القيمة الأصلية اللي تقابل bestNorm (قبل normalize)
            var index = Array.FindIndex(normLexicon, x => x == bestNorm);
            if (index >= 0 && index < lexicon.Length)
                return lexicon[index];

            return raw;
        }

        public string SmartCorrectReligion(string? raw)
            => CorrectFromLexicon(raw, OcrLexicon.Religions, maxDistance: 1);

        public string SmartCorrectMarital(string? raw)
            => CorrectFromLexicon(raw, OcrLexicon.MaritalStatuses, maxDistance: 1);

        public string SmartCorrectOccupation(string? raw)
            => CorrectFromLexicon(raw, OcrLexicon.Occupations, maxDistance: 2);

        public string SmartCorrectGovernorate(string? raw)
            => CorrectFromLexicon(raw, OcrLexicon.Governorates, maxDistance: 2);

        public string SmartCorrectName(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var norm = ArabicTextNormalizer.Normalize(raw);
            var tokens = norm
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var fixedTokens = new List<string>();

            foreach (var t in tokens)
            {
                // نحاول نصحح التوكن لو قريب من اسم شائع
                var best = Fuzzy.BestMatch(
                    t,
                    OcrLexicon.NameCommonTokens
                        .Select(ArabicTextNormalizer.Normalize),
                    maxDistance: 1
                );

                if (best == null)
                {
                    // ملاقيناش بديل قريب → استخدم التوكن كما هو
                    fixedTokens.Add(t);
                }
                else
                {
                    // رجّع الشكل الأصلي قبل normalize
                    var idx = Array.FindIndex(
                        OcrLexicon.NameCommonTokens.Select(ArabicTextNormalizer.Normalize).ToArray(),
                        x => x == best
                    );
                    if (idx >= 0)
                        fixedTokens.Add(OcrLexicon.NameCommonTokens[idx]);
                    else
                        fixedTokens.Add(t);
                }
            }

            return string.Join(" ", fixedTokens);
        }

        public string SmartCorrectAddress(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var norm = ArabicTextNormalizer.Normalize(raw);

            // مثال بسيط: نصحح اسم المحافظة جوه العنوان لو موجود
            foreach (var gov in OcrLexicon.Governorates)
            {
                var govNorm = ArabicTextNormalizer.Normalize(gov);
                if (norm.Contains(govNorm))
                {
                    // هنا ممكن تعمل replace متقدم، بس كبداية
                    // يكفي إنك تتركها زي ما هي
                }
            }

            return raw;
        }
    }
}
