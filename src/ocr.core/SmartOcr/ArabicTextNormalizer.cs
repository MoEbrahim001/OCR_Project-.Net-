using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ocr.core.SmartOcr
{
    public static class ArabicTextNormalizer
    {
        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var s = input;

            // 1) شيل التشكيل وعلامات القرآن إلخ
            s = Regex.Replace(s, "[\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED]", "");

            // 2) شيل التطويل وعلامات الاتجاه والـ zero-width
            s = Regex.Replace(s, "[\u0640\u200E\u200F\u202A-\u202E\u2066-\u2069\u200B-\u200D]", "");

            // 3) توحيد أشكال الحروف
            s = s
                .Replace('أ', 'ا')
                .Replace('إ', 'ا')
                .Replace('آ', 'ا')
                .Replace('ٱ', 'ا')
                .Replace('ى', 'ي')
                .Replace('ی', 'ي')
                .Replace('ة', 'ه')
                .Replace('ؤ', 'و')
                .Replace('ئ', 'ي')
                .Replace('گ', 'ك')
                .Replace('پ', 'ب');

            // 4) توحيد المسافات
            s = Regex.Replace(s, @"\s+", " ").Trim();

            // 5) lowercase
            return s.ToLowerInvariant();
        }
    }
}
