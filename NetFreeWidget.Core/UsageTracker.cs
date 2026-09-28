using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetFreeWidget.Core
{
    public sealed class TimeRange
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }

    public sealed class TrackingState
    {
        public DateTime? LastTime { get; set; }
        public double LastUsage { get; set; }

        /// <summary>Stretches of time proven to contain no reset (consecutive samples, close together, usage never dropped).</summary>
        public List<TimeRange> Covered { get; set; } = new();

        /// <summary>Each reset happened somewhere after <c>From</c> (last sample before the drop) and up to <c>To</c> (first sample after it).</summary>
        public List<TimeRange> Resets { get; set; } = new();
    }

    [JsonSerializable(typeof(TrackingState))]
    internal partial class TrackingJsonContext : JsonSerializerContext { }

    /// <summary>
    /// Learns the package reset day when the user does not know it, from the moments monthly usage drops back.
    /// The reset instant is only known to lie between two samples, so the result is a set of possible
    /// days of the month, narrowed by every observed reset and by every fully observed day without one.
    /// </summary>
    public static class UsageTracker
    {
        // Up to this gap a reset cannot hide: the new cycle would have to re-use a whole month's traffic in a few days.
        private static readonly TimeSpan MaxCoveredGap = TimeSpan.FromDays(4);
        private static readonly TimeSpan KeepHistory = TimeSpan.FromDays(400);
        private const int MaxResets = 12;
        private const int MaxCovered = 120;

        private static readonly string StatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetFreeWidget", "tracking.json");

        private static readonly object Gate = new();
        private static TrackingState? _state;

        /// <summary>Feeds one successful usage reading into the tracker and persists it.</summary>
        public static void Record(DateTime now, double usageGb)
        {
            lock (Gate)
            {
                var s = State();
                if (Apply(s, now, usageGb))
                    Save(s);
            }
        }

        /// <summary>Days of the month (1-31) the package may reset on; all 31 when nothing is known yet.</summary>
        public static List<int> GetCandidateDays()
        {
            lock (Gate)
                return GetCandidateDays(State());
        }

        /// <summary>Adds one reading to <paramref name="s"/>; returns false when it taught nothing.</summary>
        public static bool Apply(TrackingState s, DateTime now, double usageGb)
        {
            if (double.IsNaN(usageGb) || usageGb < 0)
                return false;

            if (s.LastTime is DateTime last)
            {
                if (now <= last)
                    return false; // clock moved back

                // Tolerate rounding in the displayed figure (e.g. MB -> GB switch); a reset drops far more.
                bool dropped = usageGb < s.LastUsage * 0.9 - 0.01;
                if (dropped)
                {
                    s.Resets.Add(new TimeRange { From = last, To = now });
                    if (s.Resets.Count > MaxResets)
                        s.Resets.RemoveAt(0);
                }
                else if (now - last <= MaxCoveredGap)
                {
                    var tail = s.Covered.Count > 0 ? s.Covered[^1] : null;
                    if (tail != null && tail.To == last)
                        tail.To = now;
                    else
                        s.Covered.Add(new TimeRange { From = last, To = now });
                }

                s.Covered.RemoveAll(r => now - r.To > KeepHistory);
                s.Resets.RemoveAll(r => now - r.To > KeepHistory);
                if (s.Covered.Count > MaxCovered)
                    s.Covered.RemoveRange(0, s.Covered.Count - MaxCovered);
            }

            s.LastTime = now;
            s.LastUsage = usageGb;
            return true;
        }

        public static List<int> GetCandidateDays(TrackingState s)
        {
            var all = Candidates(s.Resets, s.Covered);
            if (all.Count > 0 || s.Resets.Count == 0)
                return all;

            // The history contradicts itself (e.g. the package's reset day changed): trust only the latest cycle.
            var latest = s.Resets[^1];
            return Candidates(new List<TimeRange> { latest }, s.Covered.FindAll(r => r.From >= latest.From));
        }

        private static List<int> Candidates(List<TimeRange> resets, List<TimeRange> covered)
        {
            var result = new List<int>(31);
            for (int day = 1; day <= 31; day++)
            {
                if (resets.TrueForAll(r => MayResetWithin(day, r)) && covered.TrueForAll(c => !ResetsInside(day, c)))
                    result.Add(day);
            }
            return result;
        }

        /// <summary>Some calendar day touched by the window (From, To] is a reset day for this cycle day.</summary>
        private static bool MayResetWithin(int cycleDay, TimeRange window)
        {
            var first = window.From.Date;
            var last = window.To.Date;
            if ((last - first).TotalDays >= 31)
                return true;

            for (var d = first; d <= last; d = d.AddDays(1))
                if (UsageCalculator.IsCycleStart(cycleDay, d))
                    return true;
            return false;
        }

        /// <summary>A whole reset day for this cycle day lies inside a stretch known to contain no reset.</summary>
        private static bool ResetsInside(int cycleDay, TimeRange covered)
        {
            // Whole days only: the hour of the reset is unknown, so a partly observed day proves nothing.
            var d = covered.From == covered.From.Date ? covered.From : covered.From.Date.AddDays(1);
            for (; d.AddDays(1) <= covered.To; d = d.AddDays(1))
                if (UsageCalculator.IsCycleStart(cycleDay, d))
                    return true;
            return false;
        }

        private static TrackingState State()
        {
            if (_state != null)
                return _state;

            try
            {
                if (File.Exists(StatePath))
                {
                    using var stream = File.OpenRead(StatePath);
                    _state = JsonSerializer.Deserialize(stream, TrackingJsonContext.Default.TrackingState);
                }
            }
            catch (Exception ex)
            {
                Log.Error("UsageTracker", ex);
            }

            return _state ??= new TrackingState();
        }

        private static void Save(TrackingState s)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
                string tmp = StatePath + ".tmp";
                using (var stream = File.Create(tmp))
                    JsonSerializer.Serialize(stream, s, TrackingJsonContext.Default.TrackingState);
                File.Move(tmp, StatePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Error("UsageTracker", ex);
            }
        }
    }
}
