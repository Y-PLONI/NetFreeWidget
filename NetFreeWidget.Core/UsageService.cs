using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.NetworkInformation;
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
        private static Task<UsageReading>? _inflight;
        private static UsageReading _last = UsageReading.Error;
        private static long _lastFetchTick;
        private static int _failStreak;

        static UsageService()
        {
            // A new network may be a different NetFree account: drop the cached reading and any backoff.
            NetworkChange.NetworkAddressChanged += (_, _) =>
            {
                lock (Gate)
                {
                    _lastFetchTick = 0;
                    _failStreak = 0;
                }
            };
        }

        /// <summary>The account of the latest successful reading (0 when none yet).</summary>
        public static long LastUserId
        {
            get { lock (Gate) return _last.UserId; }
        }

        /// <summary>
        /// Returns the current usage, sharing one in-flight request between all callers and caching the
        /// result (10 min on success, 30s doubling up to 15 min on failure). <paramref name="force"/> bypasses
        /// the cache but still waits at least 10 seconds between requests.
        /// </summary>
        public static Task<UsageReading> GetUsageAsync(bool force = false)
        {
            lock (Gate)
            {
                if (_inflight != null)
                    return _inflight;

                if (_lastFetchTick != 0)
                {
                    var age = TimeSpan.FromMilliseconds(Environment.TickCount64 - _lastFetchTick);
                    var ttl = _last.IsError
                        ? TimeSpan.FromSeconds(Math.Min(30 << Math.Min(_failStreak - 1, 5), MaxBackoff.TotalSeconds))
                        : SuccessTtl;
                    if (age < (force ? ForceFloor : ttl))
                        return Task.FromResult(_last);
                }

                return _inflight = FetchAndStoreAsync();
            }
        }

        private static async Task<UsageReading> FetchAndStoreAsync()
        {
            var reading = UsageReading.Error;
            try
            {
                reading = await NetFreeApi.GetUsageAsync().ConfigureAwait(false);
                if (!double.IsNaN(reading.Gb))
                    UsageTracker.Record(reading.UserId, DateTime.Now, reading.Gb);
                return reading;
            }
            finally
            {
                lock (Gate)
                {
                    _last = reading;
                    _lastFetchTick = Environment.TickCount64;
                    _failStreak = reading.IsError ? _failStreak + 1 : 0;
                    _inflight = null;
                }
            }
        }

        public static async Task<UsageSnapshot> GetSnapshotAsync(WidgetSettings settings, bool force = false)
        {
            return BuildSnapshot(await GetUsageAsync(force).ConfigureAwait(false), settings);
        }

        // Beyond this spread of possible reset days a "range" says nothing useful.
        private const int MaxUsefulCandidates = 10;

        public static UsageSnapshot BuildSnapshot(UsageReading reading, WidgetSettings settings)
        {
            if (reading.NoPackage)
            {
                return new UsageSnapshot
                {
                    IsNoPackage = true,
                    IsPending = true,
                    TotalGb = settings.PackageQuotaGb,
                    StatusText = "אין חבילת גלישה מוגבלת בחיבור הזה"
                };
            }

            var learned = settings.EffectiveCycleDay() == 0 ? UsageTracker.GetCandidateDays(reading.UserId) : null;
            return BuildSnapshot(reading.Gb, settings, DateTime.Now, learned);
        }

        /// <param name="learnedDays">Possible reset days from the tracker; used only when no day is configured.</param>
        public static UsageSnapshot BuildSnapshot(double currentUsageGb, WidgetSettings settings, DateTime now, List<int>? learnedDays)
        {
            var snapshot = new UsageSnapshot();

            if (double.IsNaN(currentUsageGb))
            {
                snapshot.StatusText = "שגיאה בחיבור";
                snapshot.IsError = true;
                return snapshot;
            }

            double quota = settings.PackageQuotaGb;
            snapshot.UsedGb = currentUsageGb;
            snapshot.TotalGb = quota;

            if (quota <= 0)
            {
                snapshot.IsPending = true;
                snapshot.StatusText = "יש להגדיר את נפח החבילה";
                return snapshot;
            }

            snapshot.UsedPercent = Math.Min(currentUsageGb / quota * 100, 100);
            string usedPercent = $"נוצל {snapshot.UsedPercent:F0}%";

            // The reset day: exact when configured, otherwise whatever the tracker could narrow it down to.
            int configuredDay = settings.EffectiveCycleDay();
            var days = configuredDay > 0 ? new List<int> { configuredDay } : learnedDays ?? new List<int>();

            if (days.Count == 0 || days.Count > MaxUsefulCandidates)
            {
                snapshot.IsPending = true;
                snapshot.StatusText = "לומד את מועד האיפוס...";
                snapshot.CycleText = "האיפוס יזוהה כשהצריכה תתאפס";
                snapshot.PercentText = usedPercent;
                return snapshot;
            }

            // Evaluate every possible reset day; the true one is among them, so the result is a range.
            string mode = settings.WeekendMode;
            double expLo = double.MaxValue, expHi = double.MinValue, fracLo = 1, fracHi = 0;
            DateTime startLo = DateTime.MaxValue, startHi = DateTime.MinValue;
            DateTime? exhaustLo = null, exhaustHi = null;
            bool exhausted = false;
            DateTime today = UsageCalculator.StartOfDay(now);

            foreach (int day in days)
            {
                var (cycleStart, cycleEnd, t) = UsageCalculator.GetCycleDates(day, now);
                today = t;
                int totalUnits = UsageCalculator.CountUnits(cycleStart, cycleEnd, mode);
                int elapsedUnits = UsageCalculator.CountUnits(cycleStart, today, mode);

                double expected = totalUnits > 0 ? quota / totalUnits * elapsedUnits : quota;
                double frac = totalUnits > 0 ? (double)elapsedUnits / totalUnits : 1;
                expLo = Math.Min(expLo, expected);
                expHi = Math.Max(expHi, expected);
                fracLo = Math.Min(fracLo, frac);
                fracHi = Math.Max(fracHi, frac);
                if (cycleStart < startLo) startLo = cycleStart;
                if (cycleStart > startHi) startHi = cycleStart;

                if (elapsedUnits > 0 && currentUsageGb > expected)
                {
                    double remainingGb = quota - currentUsageGb;
                    if (remainingGb <= 0)
                    {
                        exhausted = true;
                    }
                    else if (UsageCalculator.PredictExhaustionDate(today, remainingGb, currentUsageGb / elapsedUnits, mode) is DateTime date)
                    {
                        if (exhaustLo == null || date < exhaustLo) exhaustLo = date;
                        if (exhaustHi == null || date > exhaustHi) exhaustHi = date;
                    }
                }
            }

            bool exact = startLo == startHi;
            snapshot.ExpectedText = FormatRange(expLo, expHi, "F2");
            snapshot.PercentText = $"{usedPercent} · עבר {FormatRange(fracLo * 100, fracHi * 100, "F0")}% מהחודש";
            snapshot.CycleText = configuredDay > 0
                ? $"איפוס ב-{configuredDay} לחודש"
                : exact
                    ? $"איפוס ב-{startLo.Day} לחודש (זוהה אוטומטית)"
                    : $"איפוס משוער: {FormatDays(days)} לחודש";

            // A verdict is stated only when it holds for every possible reset day; the figure is the guaranteed part.
            if (currentUsageGb > expHi)
            {
                snapshot.IsOverLimit = true;
                snapshot.StatusText = exact
                    ? $"חריגה: {(currentUsageGb - expHi):F2} GB"
                    : $"חריגה של לפחות {(currentUsageGb - expHi):F2} GB";
            }
            else if (currentUsageGb <= expLo)
            {
                snapshot.StatusText = exact
                    ? $"תקין: נותרו {(expLo - currentUsageGb):F1} GB"
                    : $"תקין: נותרו לפחות {(expLo - currentUsageGb):F1} GB";
            }
            else
            {
                snapshot.IsUncertain = true;
                snapshot.StatusText = "גבולי: תלוי במועד האיפוס המדויק";
            }

            if (exhausted)
                snapshot.ExhaustionText = "החבילה הסתיימה!";
            else if (snapshot.IsOverLimit && exhaustLo is DateTime lo && exhaustHi is DateTime hi)
                snapshot.ExhaustionText = lo == hi
                    ? $"תגמר ב{HebrewDayNames[(int)lo.DayOfWeek]} ({lo:dd/MM})"
                    : $"תגמר בין {lo:dd/MM} ל-{hi:dd/MM}";

            return snapshot;
        }

        /// <summary>Days of the month as cyclic runs, e.g. {28..31, 1..3} -> "28–3".</summary>
        private static string FormatDays(List<int> days)
        {
            var set = new bool[32];
            foreach (int d in days)
                set[d] = true;

            // Start right after a missing day so a run that wraps past the month end stays in one piece.
            int start = 1;
            for (int d = 1; d <= 31; d++)
                if (!set[d]) { start = d % 31 + 1; break; }

            var runs = new List<string>();
            for (int i = 0; i < 31;)
            {
                int d = (start - 1 + i) % 31 + 1;
                if (!set[d]) { i++; continue; }

                int first = d, last = d;
                while (++i < 31 && set[(start - 1 + i) % 31 + 1])
                    last = (start - 1 + i) % 31 + 1;
                runs.Add(first == last ? $"{first}" : $"{first}–{last}");
            }
            return string.Join(", ", runs);
        }

        private static string FormatRange(double lo, double hi, string format)
        {
            string a = lo.ToString(format, CultureInfo.InvariantCulture);
            string b = hi.ToString(format, CultureInfo.InvariantCulture);
            return a == b ? a : $"{a}–{b}";
        }
    }
}
