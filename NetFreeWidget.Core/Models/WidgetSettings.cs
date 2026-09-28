namespace NetFreeWidget.Core.Models
{
    public class WidgetSettings
    {
        public string PackageStartDate { get; set; } = "";
        public double PackageQuotaGb { get; set; } = 0;
        public string WeekendMode { get; set; } = "two";
    }
}
