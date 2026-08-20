using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Views
{
    public partial class DashboardView : UserControl, IRefreshable
    {
        private readonly SnapRepository _repository;
        private readonly DashboardAnalyticsService _analytics;
        private readonly Action<string>? _openSubscriptions;

        public DashboardView(SnapRepository repository, Action<string>? openSubscriptions = null)
        {
            InitializeComponent();
            _repository = repository;
            _analytics = new DashboardAnalyticsService();
            _openSubscriptions = openSubscriptions;
            MakeHealthIndicatorClickable(NearDueSmall, "نزدیک سررسید");
            MakeHealthIndicatorClickable(OverdueSmall, "معوق");
            MakeHealthIndicatorClickable(Suspended, "تعلیق");
            MakeHealthIndicatorClickable(Overdue, "معوق");
        }

        private void MakeHealthIndicatorClickable(TextBlock target, string status)
        {
            target.Cursor = Cursors.Hand;
            target.ToolTip = "برای دیدن فهرست مراکز کلیک کنید";
            target.MouseLeftButtonUp += (sender, args) => _openSubscriptions?.Invoke(status);
        }

        public void RefreshData()
        {
            var stats = _repository.GetDashboard();
            var trend = _analytics.GetRevenueTrend(6);

            ActiveMembers.Text = Format(stats.ActiveMembers);
            Centers.Text = Format(stats.RegisteredCenters);
            MonthRevenue.Text = Money(stats.MonthRevenue);
            Overdue.Text = Format(stats.Overdue);
            OverdueSmall.Text = Format(stats.Overdue);
            NearDueSmall.Text = Format(stats.NearDue);
            Suspended.Text = Format(stats.Suspended);
            ActiveSectors.Text = Format(stats.ActiveSectors);

            Teachers.Text = TypeCount(stats, "معلم");
            Scholars.Text = TypeCount(stats, "عالم");
            Professors.Text = TypeCount(stats, "استاد پوهنتون");
            Cultural.Text = TypeCount(stats, "فرهنگی");
            Students.Text = TypeCount(stats, "شاگرد");

            Summary.Text = Format(stats.ActiveMembers) + " عضو فعال، " + Format(stats.RegisteredCenters) +
                           " مرکز و " + Format(stats.ActiveSectors) + " سکتور فعال در سیستم ثبت است.";

            RenderRevenue(trend);
        }

        private void RenderRevenue(IReadOnlyList<RevenueTrendPoint> trend)
        {
            RevenueMonths.ItemsSource = trend;
            RevenuePointLayer.Children.Clear();

            if (trend.Count == 0)
            {
                RevenueLine.Points = new PointCollection();
                RevenueArea.Points = new PointCollection();
                SixMonthRevenue.Text = "0 افغانی";
                AverageRevenue.Text = "0 افغانی";
                PeakMonth.Text = "داده‌ای ثبت نشده";
                return;
            }

            var total = trend.Sum(item => item.Amount);
            var average = total / trend.Count;
            var peak = trend.OrderByDescending(item => item.Amount).First();
            SixMonthRevenue.Text = Money(total);
            AverageRevenue.Text = Money(average);
            PeakMonth.Text = peak.Amount <= 0 ? "داده‌ای ثبت نشده" : peak.Label + "  •  " + Money(peak.Amount);

            const double xStart = 26;
            const double xEnd = 694;
            const double yTop = 30;
            const double yBottom = 168;
            var max = trend.Max(item => item.Amount);
            if (max <= 0) max = 1;

            var linePoints = new PointCollection();
            for (var i = 0; i < trend.Count; i++)
            {
                var x = trend.Count == 1 ? (xStart + xEnd) / 2 : xStart + ((xEnd - xStart) * i / (trend.Count - 1));
                var ratio = (double)(trend[i].Amount / max);
                var y = yBottom - ((yBottom - yTop) * ratio);
                linePoints.Add(new Point(x, y));

                var dot = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.White,
                    Stroke = new SolidColorBrush(Color.FromRgb(11, 128, 109)),
                    StrokeThickness = 3,
                    ToolTip = trend[i].AmountText
                };
                Canvas.SetLeft(dot, x - 5);
                Canvas.SetTop(dot, y - 5);
                RevenuePointLayer.Children.Add(dot);

                var label = new TextBlock
                {
                    Text = Format(trend[i].Amount),
                    Width = 110,
                    TextAlignment = TextAlignment.Center,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(58, 80, 101)),
                    FlowDirection = FlowDirection.LeftToRight
                };
                Canvas.SetLeft(label, x - 55);
                Canvas.SetTop(label, Math.Max(1, y - 27));
                RevenuePointLayer.Children.Add(label);
            }

            RevenueLine.Points = linePoints;
            var areaPoints = new PointCollection { new Point(linePoints[0].X, yBottom) };
            foreach (var point in linePoints) areaPoints.Add(point);
            areaPoints.Add(new Point(linePoints[linePoints.Count - 1].X, yBottom));
            RevenueArea.Points = areaPoints;
        }

        private static string TypeCount(DashboardStats stats, string type) =>
            stats.MemberTypes.ContainsKey(type) ? Format(stats.MemberTypes[type]) : "0";

        private static string Format(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
        private static string Format(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture);
        private static string Money(long value) => Format(value) + " افغانی";
        private static string Money(decimal value) => Format(value) + " افغانی";
    }
}