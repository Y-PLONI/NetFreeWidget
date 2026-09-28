namespace NetFreeWidget.Core.Models
{
    public sealed class UsageSnapshot
    {
        public double UsedGb { get; set; }
        public double TotalGb { get; set; }
        public double ExpectedGb { get; set; }
        public bool IsOverLimit { get; set; }
        public bool IsError { get; set; }
        public string StatusText { get; set; } = "";
        public string? ExhaustionText { get; set; }
        public double UsedPercent { get; set; }
    }
}
