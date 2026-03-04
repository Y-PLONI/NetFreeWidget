using System;

namespace NetFreeWidget
{
    public class UsageCalculator
    {
        public static DateTime StartOfDay(DateTime d)
        {
            return new DateTime(d.Year, d.Month, d.Day);
        }

        public static DateTime AddDays(DateTime d, int num)
        {
            return StartOfDay(d.AddDays(num));
        }

        public static int CountUnits(DateTime startDate, DateTime endDate, string weekendMode)
        {
            var start = StartOfDay(startDate);
            var end = StartOfDay(endDate);
            if (start > end) return 0;

            int count = 0;
            var cursor = start;

            while (cursor <= end)
            {
                int day = (int)cursor.DayOfWeek; // 0=Sunday, 5=Friday, 6=Saturday
                
                if (weekendMode == "one")
                {
                    // שישי ושבת כיום אחד
                    if (day == 5 && AddDays(cursor, 1) <= end)
                    {
                        count++;
                        cursor = AddDays(cursor, 2);
                        continue;
                    }
                    if (day == 6)
                    {
                        count++;
                        cursor = AddDays(cursor, 1);
                        continue;
                    }
                }
                
                count++;
                cursor = AddDays(cursor, 1);
            }

            return count;
        }

        public static (DateTime cycleStart, DateTime cycleEnd, DateTime today)? GetCycleDates(string packageStartDateStr)
        {
            if (string.IsNullOrEmpty(packageStartDateStr))
                return null;

            if (!DateTime.TryParse(packageStartDateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime initStart))
                return null;

            var today = StartOfDay(DateTime.Now);
            int desiredDay = initStart.Day;

            // מציאת תחילת מחזור נוכחי
            int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
            var cycleStart = new DateTime(today.Year, today.Month, Math.Min(desiredDay, daysInMonth));
            
            if (cycleStart > today)
            {
                int prevMonth = today.Month - 1;
                int prevYear = today.Year;
                if (prevMonth < 1)
                {
                    prevMonth = 12;
                    prevYear--;
                }
                int daysInPrevMonth = DateTime.DaysInMonth(prevYear, prevMonth);
                cycleStart = new DateTime(prevYear, prevMonth, Math.Min(desiredDay, daysInPrevMonth));
            }

            // חישוב סוף מחזור
            int nextMonth = cycleStart.Month + 1;
            int nextYear = cycleStart.Year;
            if (nextMonth > 12)
            {
                nextMonth = 1;
                nextYear++;
            }
            int daysInNextMonth = DateTime.DaysInMonth(nextYear, nextMonth);
            var nextCycleStart = new DateTime(nextYear, nextMonth, Math.Min(desiredDay, daysInNextMonth));
            var cycleEnd = AddDays(nextCycleStart, -1);

            return (cycleStart, cycleEnd, today);
        }

        public static DateTime? PredictExhaustionDate(DateTime today, double remainingGb, double ratePerUnit, string weekendMode)
        {
            if (ratePerUnit <= 0) return null;
            
            double unitsLeft = remainingGb / ratePerUnit;
            if (unitsLeft <= 0) return today;

            var cursor = today;
            int wholeUnits = (int)Math.Ceiling(unitsLeft);

            while (wholeUnits > 0)
            {
                cursor = cursor.AddDays(1);
                
                if (weekendMode == "one" && cursor.DayOfWeek == DayOfWeek.Saturday)
                {
                    // שבת לא מבזבז יחידה נוספת
                }
                else
                {
                    wholeUnits--;
                }
            }

            return cursor;
        }
    }
}
