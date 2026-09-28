using System;
using System.Globalization;

namespace NetFreeWidget.Core.Models
{
    public class WidgetSettings
    {
        /// <summary>Legacy field (yyyy-MM-dd); only its day-of-month was ever used. Superseded by <see cref="CycleDay"/>.</summary>
        public string PackageStartDate { get; set; } = "";

        /// <summary>Day of month the package resets on (1-31), or 0 when unknown and detected automatically.</summary>
        public int CycleDay { get; set; } = 0;

        public double PackageQuotaGb { get; set; } = 0;
        public string WeekendMode { get; set; } = "two";

        /// <summary>The configured reset day, falling back to the legacy start date; 0 means "detect automatically".</summary>
        public int EffectiveCycleDay()
        {
            if (CycleDay is >= 1 and <= 31)
                return CycleDay;

            if (!string.IsNullOrEmpty(PackageStartDate) &&
                DateTime.TryParse(PackageStartDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d.Day;

            return 0;
        }
    }
}
