namespace NetFreeWidget.Core.Models
{
    public sealed class UsageSnapshot
    {
        public double UsedGb { get; set; }
        public double TotalGb { get; set; }
        public bool IsOverLimit { get; set; }
        public bool IsError { get; set; }

        /// <summary>The reset day is only known as a range and the verdict differs across it.</summary>
        public bool IsUncertain { get; set; }

        /// <summary>No verdict can be given yet (quota missing, or the reset day is still being learned).</summary>
        public bool IsPending { get; set; }

        public string StatusText { get; set; } = "";
        public string? ExhaustionText { get; set; }

        /// <summary>"12.34" or "11.20–13.90": the usage that is on track for today.</summary>
        public string ExpectedText { get; set; } = "";

        /// <summary>"נוצל 45% · עבר 40%–47% מהחודש".</summary>
        public string PercentText { get; set; } = "";

        /// <summary>When the package resets, as far as it is known.</summary>
        public string CycleText { get; set; } = "";

        public double UsedPercent { get; set; }
    }
}
