using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SnapAfghanistan.Native.Models;

namespace SnapAfghanistan.Native.Services
{
    public sealed class ReportService
    {
        public void ExportCsv(DataTable table, string path)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => Csv(c.ColumnName))));
                foreach (DataRow row in table.Rows)
                    writer.WriteLine(string.Join(",", row.ItemArray.Select(value => Csv(Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""))));
            }
        }

        public void ExportTablePdf(DataTable table, string path, string title, string companyName)
        {
            if (table.Columns.Count == 0) throw new InvalidOperationException("گزارش خالی است.");
            var document = new PdfDocument();
            document.Info.Title = title;
            document.Info.Author = companyName;

            PdfPage? page = null;
            XGraphics? graphics = null;
            var font = CreateFont(7.5, false);
            var headerFont = CreateFont(7.5, true);
            var titleFont = CreateFont(17, true);
            var metaFont = CreateFont(8, false);
            var y = 0d;
            var rowHeight = 25d;

            Action newPage = () =>
            {
                graphics?.Dispose();
                page = document.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                page.Orientation = PdfSharp.PageOrientation.Landscape;
                graphics = XGraphics.FromPdfPage(page);
                y = DrawPageHeader(graphics, page, title, companyName, titleFont, metaFont);
                DrawTableHeader(graphics, page, table, headerFont, y, rowHeight);
                y += rowHeight;
            };

            newPage();
            var rowIndex = 0;
            foreach (DataRow row in table.Rows)
            {
                if (page == null || graphics == null) break;
                if (y + rowHeight > page.Height.Point - 35) newPage();
                DrawTableRow(graphics!, page!, row, font, y, rowHeight, rowIndex % 2 == 0);
                y += rowHeight;
                rowIndex++;
            }
            graphics?.Dispose();
            document.Save(path);
        }

        public void ExportMemberProfile(MemberRecord member, string path, string companyName)
        {
            var document = new PdfDocument();
            document.Info.Title = "پرونده عضو " + member.Code;
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            using (var graphics = XGraphics.FromPdfPage(page))
            {
                var titleFont = CreateFont(18, true);
                var labelFont = CreateFont(10, true);
                var valueFont = CreateFont(10, false);
                var y = DrawPageHeader(graphics, page, "پرونده انفرادی عضو", companyName, titleFont, CreateFont(8, false));
                var fields = new[]
                {
                    new[] { "کد عضویت", member.Code }, new[] { "گروه", member.Type },
                    new[] { "نام", member.FirstName }, new[] { "نام پدر", member.FatherName },
                    new[] { "شماره تذکره", member.TazkiraNo }, new[] { "موبایل", member.Phone },
                    new[] { "آدرس اصلی", member.OriginalAddress }, new[] { "آدرس فعلی", member.CurrentAddress },
                    new[] { "اداره / مکتب", member.Institution }, new[] { "وضعیت", member.Status },
                    new[] { "تاریخ ثبت", member.CreatedAt }, new[] { "آخرین ویرایش", member.UpdatedAt },
                    new[] { "توضیحات", member.Notes }
                };
                const double margin = 42;
                var width = page.Width.Point - margin * 2;
                foreach (var field in fields)
                {
                    var height = field[0] == "توضیحات" ? 60d : 38d;
                    graphics.DrawRoundedRectangle(new XPen(XColor.FromArgb(222, 230, 238), .7), XBrushes.White,
                        margin, y, width, height, 7, 7);
                    graphics.DrawString(PersianPdf.Shape(field[0]), labelFont, new XSolidBrush(XColor.FromArgb(46, 67, 91)),
                        new XRect(page.Width.Point - margin - 145, y, 130, height), XStringFormats.CenterRight);
                    graphics.DrawString(PersianPdf.Shape(Shorten(field[1], field[0] == "توضیحات" ? 140 : 80)), valueFont,
                        new XSolidBrush(XColor.FromArgb(22, 33, 49)), new XRect(margin + 15, y, width - 175, height), XStringFormats.CenterRight);
                    y += height + 7;
                }
            }
            document.Save(path);
        }

        private static double DrawPageHeader(XGraphics graphics, PdfPage page, string title, string companyName, XFont titleFont, XFont metaFont)
        {
            const double margin = 35;
            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "snap-logo.png");
            if (File.Exists(logoPath))
            {
                using (var logo = XImage.FromFile(logoPath)) graphics.DrawImage(logo, page.Width.Point - 125, 20, 90, 58);
            }
            graphics.DrawString(PersianPdf.Shape(companyName), titleFont, new XSolidBrush(XColor.FromArgb(15, 56, 79)),
                new XRect(margin, 20, page.Width.Point - 190, 28), XStringFormats.CenterRight);
            graphics.DrawString(PersianPdf.Shape(title), CreateFont(11, true), new XSolidBrush(XColor.FromArgb(177, 35, 57)),
                new XRect(margin, 50, page.Width.Point - 190, 20), XStringFormats.CenterRight);
            var date = "هجری شمسی " + DateService.Solar(DateTime.Today) + "   |   میلادی " + DateService.Gregorian(DateTime.Today);
            graphics.DrawString(PersianPdf.Shape(date), metaFont, new XSolidBrush(XColor.FromArgb(95, 107, 123)),
                new XRect(margin, 74, page.Width.Point - margin * 2, 18), XStringFormats.CenterRight);
            graphics.DrawLine(new XPen(XColor.FromArgb(197, 208, 219), .8), margin, 98, page.Width.Point - margin, 98);
            return 112;
        }

        private static void DrawTableHeader(XGraphics graphics, PdfPage page, DataTable table, XFont font, double y, double height)
        {
            var widths = ColumnWidths(page, table.Columns.Count);
            var x = page.Width.Point - 35;
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var width = widths[i];
                x -= width;
                graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(21, 50, 74)), x, y, width, height);
                graphics.DrawString(PersianPdf.Shape(table.Columns[i].ColumnName), font, XBrushes.White,
                    new XRect(x + 3, y, width - 6, height), XStringFormats.Center);
            }
        }

        private static void DrawTableRow(XGraphics graphics, PdfPage page, DataRow row, XFont font, double y, double height, bool alternate)
        {
            var widths = ColumnWidths(page, row.Table.Columns.Count);
            var x = page.Width.Point - 35;
            var fill = alternate ? new XSolidBrush(XColor.FromArgb(245, 248, 251)) : XBrushes.White;
            var pen = new XPen(XColor.FromArgb(220, 227, 234), .45);
            for (var i = 0; i < row.Table.Columns.Count; i++)
            {
                var width = widths[i];
                x -= width;
                graphics.DrawRectangle(pen, fill, x, y, width, height);
                var text = Shorten(Convert.ToString(row[i], CultureInfo.InvariantCulture) ?? "", Math.Max(9, (int)(width / 4.8)));
                graphics.DrawString(PersianPdf.Shape(text), font, new XSolidBrush(XColor.FromArgb(28, 42, 58)),
                    new XRect(x + 3, y, width - 6, height), XStringFormats.Center);
            }
        }

        private static double[] ColumnWidths(PdfPage page, int count)
        {
            var usable = page.Width.Point - 70;
            var widths = Enumerable.Repeat(usable / Math.Max(1, count), count).ToArray();
            if (count >= 8)
            {
                widths[0] *= 1.05;
                widths[Math.Min(6, count - 1)] *= 1.35;
                var sum = widths.Sum();
                for (var i = 0; i < widths.Length; i++) widths[i] *= usable / sum;
            }
            return widths;
        }

        private static XFont CreateFont(double size, bool bold)
        {
            var options = new XPdfFontOptions(PdfFontEncoding.Unicode, PdfFontEmbedding.Always);
            return new XFont("Tahoma", size, bold ? XFontStyle.Bold : XFontStyle.Regular, options);
        }

        private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        private static string Shorten(string value, int max) => value.Length <= max ? value : value.Substring(0, Math.Max(1, max - 1)) + "…";
    }

    internal static class PersianPdf
    {
        private sealed class Forms
        {
            public char Isolated, Final, Initial, Medial;
            public bool JoinPrevious, JoinNext;
            public Forms(int isolated, int final, int initial = 0, int medial = 0)
            {
                Isolated = (char)isolated; Final = (char)final;
                Initial = initial == 0 ? (char)isolated : (char)initial;
                Medial = medial == 0 ? (char)final : (char)medial;
                JoinPrevious = final != 0;
                JoinNext = initial != 0;
            }
        }

        private static readonly Dictionary<char, Forms> Map = new Dictionary<char, Forms>
        {
            ['ء']=new Forms(0xFE80,0), ['آ']=new Forms(0xFE81,0xFE82), ['أ']=new Forms(0xFE83,0xFE84), ['ؤ']=new Forms(0xFE85,0xFE86),
            ['ئ']=new Forms(0xFE89,0xFE8A,0xFE8B,0xFE8C), ['ا']=new Forms(0xFE8D,0xFE8E), ['ب']=new Forms(0xFE8F,0xFE90,0xFE91,0xFE92),
            ['پ']=new Forms(0xFB56,0xFB57,0xFB58,0xFB59), ['ة']=new Forms(0xFE93,0xFE94), ['ت']=new Forms(0xFE95,0xFE96,0xFE97,0xFE98),
            ['ث']=new Forms(0xFE99,0xFE9A,0xFE9B,0xFE9C), ['ج']=new Forms(0xFE9D,0xFE9E,0xFE9F,0xFEA0), ['چ']=new Forms(0xFB7A,0xFB7B,0xFB7C,0xFB7D),
            ['ح']=new Forms(0xFEA1,0xFEA2,0xFEA3,0xFEA4), ['خ']=new Forms(0xFEA5,0xFEA6,0xFEA7,0xFEA8), ['د']=new Forms(0xFEA9,0xFEAA),
            ['ذ']=new Forms(0xFEAB,0xFEAC), ['ر']=new Forms(0xFEAD,0xFEAE), ['ز']=new Forms(0xFEAF,0xFEB0), ['ژ']=new Forms(0xFB8A,0xFB8B),
            ['س']=new Forms(0xFEB1,0xFEB2,0xFEB3,0xFEB4), ['ش']=new Forms(0xFEB5,0xFEB6,0xFEB7,0xFEB8), ['ص']=new Forms(0xFEB9,0xFEBA,0xFEBB,0xFEBC),
            ['ض']=new Forms(0xFEBD,0xFEBE,0xFEBF,0xFEC0), ['ط']=new Forms(0xFEC1,0xFEC2,0xFEC3,0xFEC4), ['ظ']=new Forms(0xFEC5,0xFEC6,0xFEC7,0xFEC8),
            ['ع']=new Forms(0xFEC9,0xFECA,0xFECB,0xFECC), ['غ']=new Forms(0xFECD,0xFECE,0xFECF,0xFED0), ['ف']=new Forms(0xFED1,0xFED2,0xFED3,0xFED4),
            ['ق']=new Forms(0xFED5,0xFED6,0xFED7,0xFED8), ['ك']=new Forms(0xFED9,0xFEDA,0xFEDB,0xFEDC), ['ک']=new Forms(0xFB8E,0xFB8F,0xFB90,0xFB91),
            ['گ']=new Forms(0xFB92,0xFB93,0xFB94,0xFB95), ['ل']=new Forms(0xFEDD,0xFEDE,0xFEDF,0xFEE0), ['م']=new Forms(0xFEE1,0xFEE2,0xFEE3,0xFEE4),
            ['ن']=new Forms(0xFEE5,0xFEE6,0xFEE7,0xFEE8), ['ه']=new Forms(0xFEE9,0xFEEA,0xFEEB,0xFEEC), ['و']=new Forms(0xFEED,0xFEEE),
            ['ى']=new Forms(0xFEEF,0xFEF0), ['ي']=new Forms(0xFEF1,0xFEF2,0xFEF3,0xFEF4), ['ی']=new Forms(0xFBFC,0xFBFD,0xFBFE,0xFBFF),
            ['ۀ']=new Forms(0xFBA4,0xFBA5)
        };

        public static string Shape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var shaped = new char[text.Length];
            for (var i = 0; i < text.Length; i++)
            {
                Forms current;
                if (!Map.TryGetValue(text[i], out current!)) { shaped[i] = text[i]; continue; }
                Forms previous = null!;
                Forms next = null!;
                var joinPrevious = i > 0 && Map.TryGetValue(text[i - 1], out previous!) && current.JoinPrevious && previous.JoinNext;
                var joinNext = i + 1 < text.Length && Map.TryGetValue(text[i + 1], out next!) && current.JoinNext && next.JoinPrevious;
                shaped[i] = joinPrevious && joinNext ? current.Medial : joinPrevious ? current.Final : joinNext ? current.Initial : current.Isolated;
            }
            Array.Reverse(shaped);
            var result = new string(shaped);
            return Regex.Replace(result, @"[A-Za-z0-9][A-Za-z0-9.,:/_\-]*", match => new string(match.Value.Reverse().ToArray()));
        }
    }
}
