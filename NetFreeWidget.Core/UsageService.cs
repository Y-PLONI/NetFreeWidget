using System;
using System.Globalization;
using System.Threading.Tasks;
using NetFreeWidget.Core.Models;

namespace NetFreeWidget.Core
{
    public static class UsageService
    {
        // Hard-coded so the process can run with InvariantGlobalization (no ICU load).
        private static readonly string[] HebrewDayNames = { "יום ראשון", "יום שני", "יום שלישי", "יום רביעי", "יום חמישי", "יום שישי", "שבת" };

        private static readonly TimeSpan SuccessTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ForceFloor = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(15);

        private static readonly object Gate = new();
        private static Task<double>? _inflight;
        private static double _lastUsage = double.NaN;
        private static long _lastFetchTick;
        private static int _failStreak;

        /// <summary>
        /// Returns the current usage, sharing one in-flight request between all callers and caching the
        /// result (10 min on success, 30s doubling up to 15 min on failure). <paramref name="force"/> bypasses
        /// the cache but still waits at least 10 seconds between requests.
        /// </summary>
        public static Task<double> GetUsageGbAsync(bool force = false)
        {
            lock (Gate)
            {
                if (_inflight != null)
                    return _inflight;

                if (_lastFetchTick != 0)
                {
                    var age = TimeSpan.FromMilliseconds(Environment.TickCount64 - _lastFetchTick);
                    var ttl = double.IsNaN(_lastUsage)
                        ? TimeSpan.FromSeconds(Math.Min(30 << Math.Min(_failStreak - 1, 5), MaxBackoff.TotalSeconds))
                        : SuccessTtl;
                    if (age < (force ? ForceFloor : ttl))
                        return Task.FromResult(_lastUsage);
                }

                return _inflight = FetchAndStoreAsync();
            }
        }

        private static async Task<double> FetchAndStoreAsync()
        {
            double usage = double.NaN;
            try
            {
                usage = await NetFreeApi.GetUsageGb().ConfigureAwait(false);
                return usage;
            }
            finally
            {
                lock (Gate)
                {
                    _lastUsage = usage;
                    _lastFetchTick = Environment.TickCount64;
                    _failStreak = double.IsNaN(usage) ? _failStreak + 1 : 0;
                    _inflight = null;
                }
            }
        }

        public static async Task<UsageSnapshot> GetSnapshotAsync(WidgetSettings settings, bool force = false)
        {
            return BuildSnapshot(await GetUsageGbAsync(force).ConfigureAwait(false), settings);
        }

        public static UsageSnapshot BuildSnapshot(double currentUsageGb, WidgetSettings settings)
        {
            var snapshot = new UsageSnapshot();

            if (double.IsNaN(currentUsageGb))
            {
                snapshot.StatusText = "שגיאה בחיבור";
                snapshot.IsError = true;
                return snapshot;
            }

            snapshot.UsedGb = currentUsageGb;
            snapshot.TotalGb = settings.PackageQuotaGb;

            var dates = UsageCalculator.GetCycleDates(settings.PackageStartDate);

            if (settings.PackageQuotaGb <= 0 || dates == null)
            {
                snapshot.StatusText = "חסרים נתונים להגדרה";
                return snapshot;
            }

            var (cycleStart, cycleEnd, today) = dates.Value;
            int totalUnits = UsageCalculator.CountUnits(cycleStart, cycleEnd, settings.WeekendMode);
            int elapsedUnits = UsageCalculator.CountUnits(cycleStart, today, settings.WeekendMode);

            double expectedGb = totalUnits > 0 ? (settings.PackageQuotaGb / totalUnits) * elapsedUnits : settings.PackageQuotaGb;
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
                            string dayName = HebrewDayNames[(int)exhaustDate.Value.DayOfWeek];
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
                snapshot.StatusText = $"תקין: נותרו {(expectedGb - currentUsageGb):F1} GB";
            }

            return snapshot;
        }
    }
}
