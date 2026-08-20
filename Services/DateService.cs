using System;
using System.Globalization;

namespace SnapAfghanistan.Native.Services
{
    public static class DateService
    {
        private static readonly PersianCalendar Calendar = new PersianCalendar();

        public static string Solar(DateTime date)
        {
            return Calendar.GetYear(date).ToString("0000", CultureInfo.InvariantCulture) + "/" +
                   Calendar.GetMonth(date).ToString("00", CultureInfo.InvariantCulture) + "/" +
                   Calendar.GetDayOfMonth(date).ToString("00", CultureInfo.InvariantCulture);
        }

        public static string SolarMonth(DateTime date)
        {
            return Calendar.GetYear(date).ToString("0000", CultureInfo.InvariantCulture) + "/" +
                   Calendar.GetMonth(date).ToString("00", CultureInfo.InvariantCulture);
        }

        public static string SolarFromIso(string value)
        {
            DateTime date;
            return TryParseIso(value, out date) ? Solar(date) : "";
        }

        public static string Gregorian(DateTime date) => date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

        public static bool TryParseIso(string value, out DateTime date)
        {
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        public static bool TryParseSolar(string value, out DateTime date)
        {
            date = default(DateTime);
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Trim().Replace('-', '/');
            var parts = normalized.Split('/');
            int year, month, day;
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out year) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out month) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out day)) return false;
            try
            {
                date = Calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        public static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}