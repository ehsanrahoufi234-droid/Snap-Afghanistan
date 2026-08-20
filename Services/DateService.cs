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

        public static string Gregorian(DateTime date) => date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

        public static bool TryParseIso(string value, out DateTime date)
        {
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        public static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
