using System;
using System.Collections.Generic;
using System.Data.SQLite;
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
            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var startMonth = currentMonth.AddMonths(-(months - 1));
            var endMonth = currentMonth.AddMonths(1);
            var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);

            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT substr(payment_date,1,7) AS month_key, COALESCE(SUM(amount),0)
FROM subscription_payments
WHERE payment_date>=@start AND payment_date<@end
GROUP BY substr(payment_date,1,7)
ORDER BY month_key";
                command.Parameters.AddWithValue("@start", startMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("@end", endMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var key = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
                        var amount = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
                        if (!string.IsNullOrWhiteSpace(key)) totals[key] = amount;
                    }
                }
            }

            var result = new List<RevenueTrendPoint>(months);
            for (var i = 0; i < months; i++)
            {
                var month = startMonth.AddMonths(i);
                var key = month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                decimal amount;
                totals.TryGetValue(key, out amount);
                result.Add(new RevenueTrendPoint
                {
                    MonthKey = key,
                    Label = month.ToString("yyyy/MM", CultureInfo.InvariantCulture),
                    Amount = amount
                });
            }
            return result;
        }
    }
}
