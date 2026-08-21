using System;
using System.Collections.Generic;
using System.Globalization;
using SnapAfghanistan.Native.Data;
using SnapAfghanistan.Native.Models;

namespace SnapAfghanistan.Native.Services
{
    public sealed class DashboardAnalyticsService
    {
        public IReadOnlyList<RevenueTrendPoint> GetRevenueTrend(int months = 6)
        {
            months = Math.Max(3, Math.Min(12, months));
            var calendar = new PersianCalendar();
            var today = DateTime.Today;
            var currentMonth = calendar.ToDateTime(calendar.GetYear(today), calendar.GetMonth(today), 1, 0, 0, 0, 0);
            var startMonth = calendar.AddMonths(currentMonth, -(months - 1));
            var endMonth = calendar.AddMonths(currentMonth, 1);
            var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);

            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT sp.payment_date, sp.amount
FROM subscription_payments sp
JOIN partners p ON p.id=sp.partner_id
WHERE p.deleted_at IS NULL
  AND sp.payment_date>=@start
  AND sp.payment_date<@end
ORDER BY sp.payment_date";
                command.Parameters.AddWithValue("@start", DateService.Iso(startMonth));
                command.Parameters.AddWithValue("@end", DateService.Iso(endMonth));
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var rawDate = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
                        DateTime paymentDate;
                        if (!DateService.TryParseIso(rawDate, out paymentDate)) continue;
                        var key = SolarMonthKey(calendar, paymentDate);
                        var amount = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
                        decimal current;
                        totals.TryGetValue(key, out current);
                        totals[key] = current + amount;
                    }
                }
            }

            var result = new List<RevenueTrendPoint>(months);
            for (var i = 0; i < months; i++)
            {
                var month = calendar.AddMonths(startMonth, i);
                var key = SolarMonthKey(calendar, month);
                decimal amount;
                totals.TryGetValue(key, out amount);
                result.Add(new RevenueTrendPoint
                {
                    MonthKey = key,
                    Label = key.Replace('-', '/'),
                    Amount = amount
                });
            }
            return result;
        }

        private static string SolarMonthKey(PersianCalendar calendar, DateTime date)
        {
            return calendar.GetYear(date).ToString("0000", CultureInfo.InvariantCulture) + "-" +
                   calendar.GetMonth(date).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
