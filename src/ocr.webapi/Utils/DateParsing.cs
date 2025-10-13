using System.Globalization;


namespace ocr.webapi.Utils
{
    public static class DateParsing
    {
        public static DateOnly? ParseYmd(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
} 
