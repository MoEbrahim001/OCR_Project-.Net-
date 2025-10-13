using System.Text;
using System.Text.Json;
using Ocr.Domain.Dtos;
using Ocr.Domain.Services;
using System.Text.RegularExpressions;

namespace Ocr.Core.Services
{
    public class OcrParser : IOcrParser
    {
        private static readonly JsonSerializerOptions Opt = new() { PropertyNameCaseInsensitive = true };

        public FrontOcrResult ParseFront(OcrExtraction raw)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(raw.RawJson, Opt)
            ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            string? Get(params string[] keys)
            {
                foreach (var k in keys)
                    if (dict.TryGetValue(k, out var v))
                        return v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
                return null;
            }




            // Map Python keys → our DTO
            var name = Clean(Get("name", "Name"));
            var address = Clean(Get("address", "Address"));
            var idRaw = Get("ID", "Id", "NationalID", "national_id", "nationalId");
            var dob = Clean(Get("DOB", "dob", "dateOfBirth"));

            var nationalId = OnlyAsciiDigits(ConvertArabicDigits(idRaw));
            int? age = TryCalcAge(dob);

          

            return new FrontOcrResult(
                Name: name,
                NationalId: nationalId,
                Address: address,
                Dob: dob,
                Age: age
            );
        }

        public BackOcrResult ParseBack(OcrExtraction raw)
        {
            using var doc = JsonDocument.Parse(raw.RawJson);
            var root = doc.RootElement;

            string? Get(params string[] keys)
            {
                foreach (var k in keys)
                    if (root.TryGetProperty(k, out var v))
                        return v.GetString();
                return null;
            }

            // Map Python keys (snake_case) → our DTO (camelCase)
            var proffession = Clean(Get("profession", "proffession"));
            var gender = Clean(Get("gender"));
            var religion = Clean(Get("religion"));
            var marital = Clean(Get("marital_status", "maritalStatus"));
            var husbandName = Clean(Get("husband_name", "husbandName"));
            var expiry = NormalizeExpiry(Get("enddate", "expiryDate", "validTo"));

            return new BackOcrResult(
                proffession: proffession,
                Gender: gender,
                Religion: religion,
                MaritalStatus: marital,
                HusbandName: husbandName,
                ExpiryDate: expiry
            );
        }

        // -------- helpers --------
        private static string? ConvertArabicDigits(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            var map = new Dictionary<char, char>
            {
                ['٠'] = '0',
                ['١'] = '1',
                ['٢'] = '2',
                ['٣'] = '3',
                ['٤'] = '4',
                ['٥'] = '5',
                ['٦'] = '6',
                ['٧'] = '7',
                ['٨'] = '8',
                ['٩'] = '9'
            };
            var sb = new StringBuilder(s!.Length);
            foreach (var ch in s)
                sb.Append(map.TryGetValue(ch, out var ascii) ? ascii : ch);
            return sb.ToString();
        }

        private static string? OnlyAsciiDigits(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            var sb = new StringBuilder();
            foreach (var ch in s!)
                if (ch >= '0' && ch <= '9') sb.Append(ch);
            return sb.Length > 0 ? sb.ToString() : null;
        }

        private static int? TryCalcAge(string? isoDate)
        {
            if (string.IsNullOrWhiteSpace(isoDate)) return null;
            if (!DateTime.TryParse(isoDate, out var birth)) return null;
            var today = DateTime.UtcNow.Date;
            var age = today.Year - birth.Year - (today < birth.AddYears(today.Year - birth.Year) ? 1 : 0);
            return age >= 0 ? age : null;
        }
        private static string? Clean(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.ReplaceLineEndings(" ").Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s;
        }

        private static string? CleanArabic(string? s)
        {
            // convert Arabic-Indic digits, then clean spaces/linebreaks
            return Clean(ConvertArabicDigits(s));
        }

        // Try to coerce something like "2026\n-88\n-83\n" into "2026-08-03"
        private static string? NormalizeExpiry(string? s)
        {
            s = CleanArabic(s);
            if (string.IsNullOrEmpty(s)) return null;

            // pick first year and two following month/day-ish numbers
            var m = Regex.Matches(s, @"\d+");
            if (m.Count >= 3)
            {
                // pick plausible yyyy, mm, dd
                var nums = m.Select(mm => mm.Value).ToList();
                var year = nums.FirstOrDefault(x => x.Length == 4) ?? nums[0];
                var idxYear = nums.IndexOf(year);
                string mm = "01", dd = "01";

                // find two nums after year if possible
                if (idxYear >= 0 && idxYear + 2 < nums.Count) { mm = nums[idxYear + 1]; dd = nums[idxYear + 2]; }
                else if (nums.Count >= 3) { mm = nums[1]; dd = nums[2]; }

                if (int.TryParse(year, out var y) && int.TryParse(mm, out var mi) && int.TryParse(dd, out var di))
                {
                    mi = Math.Clamp(mi, 1, 12);
                    di = Math.Clamp(di, 1, 31);
                    return $"{y:D4}-{mi:D2}-{di:D2}";
                }
            }
            // fallback: just return cleaned s
            return s;
        }
    }
}
