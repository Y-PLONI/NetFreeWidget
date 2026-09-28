using System;
using System.Globalization;
using System.Threading.Tasks;
using NetFreeWidget.Core.Models;

namespace NetFreeWidget.Core
{
    public class UsageService
    {
        public static async Task<UsageSnapshot> GetSnapshotAsync(WidgetSettings settings)
        {
            var snapshot = new UsageSnapshot();
            
            double currentUsageGb = await NetFreeApi.GetUsageGb();
            
            if (double.IsNaN(currentUsageGb))
            {
                snapshot.StatusText = "שגיאה בחיבור";
                snapshot.IsOverLimit = true;
                return snapshot;
            }

            snapshot.UsedGb = currentUsageGb;
            snapshot.TotalGb = settings.PackageQuotaGb;

            var dates = UsageCalculator.GetCycleDates(settings.PackageStartDate);
            
            if (settings.PackageQuotaGb <= 0 || dates == null)
            {
                snapshot.StatusText = "חסרים נתונים להגדרה";
                snapshot.IsOverLimit = false;
                return snapshot;
            }

            var (cycleStart, cycleEnd, today) = dates.Value;
            int totalUnits = UsageCalculator.CountUnits(cycleStart, cycleEnd, settings.WeekendMode);
            int elapsedUnits = UsageCalculator.CountUnits(cycleStart, today, settings.WeekendMode);

            double expectedGb = (settings.PackageQuotaGb / totalUnits) * elapsedUnits;
            snapshot.ExpectedGb = expectedGb;
            snapshot.UsedPercent = Math.Min((currentUsageGb / settings.PackageQuotaGb) * 100, 100);

            if (currentUsageGb > expectedGb)
            {
                snapshot.IsOverLimit = true;
                snapshot.StatusText = $"חריגה: {(currentUsageGb - expectedGb):F2} GB";

                if (elapsedUnits > 0)
                {
                    double ratePerUnit = currentUsageGb / elapsedUnits;
                    double remainingGb = settings.PackageQuotaGb - currentUsageGb;

                    if (remainingGb > 0)
                    {
                        var exhaustDate = UsageCalculator.PredictExhaustionDate(today, remainingGb, ratePerUnit, settings.WeekendMode);
                        if (exhaustDate.HasValue)
                        {
                            string dayName = exhaustDate.Value.ToString("dddd", new CultureInfo("he-IL"));
                            string dateStr = exhaustDate.Value.ToString("dd/MM", CultureInfo.InvariantCulture);
                            snapshot.ExhaustionText = $"תגמר ב{dayName} ({dateStr})";
                        }
                    }
                    else
                    {
                        snapshot.ExhaustionText = "החבילה הסתיימה!";
                    }
                }
            }
            else
            {
                snapshot.IsOverLimit = false;
                snapshot.StatusText = $"תקין: נותרו {(expectedGb - currentUsageGb):F1} GB";
            }

            return snapshot;
        }
    }
}
