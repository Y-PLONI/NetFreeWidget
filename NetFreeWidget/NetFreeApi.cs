using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NetFreeWidget
{
    public class NetFreeApi
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task<double> GetUsageGb()
        {
            try
            {
                // שלב 1: קבלת enc
                string userUrl = $"https://user-info.internal.netfree.link/user/{new Random().NextDouble()}";
                string userResponse = await client.GetStringAsync(userUrl);
                var userObj = JObject.Parse(userResponse);
                string enc = userObj["enc"]?.ToString() ?? "";

                if (string.IsNullOrEmpty(enc))
                    throw new Exception("Failed to get enc");

                // שלב 2: קבלת נתוני שימוש
                var payload = new { enc = enc };
                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                string linkInfoUrl = "https://netfree.link/api/user/link-info";
                var response = await client.PostAsync(linkInfoUrl, content);
                string linkInfoResponse = await response.Content.ReadAsStringAsync();
                
                var linkInfo = JObject.Parse(linkInfoResponse);
                string monthlyUsages = linkInfo["monthlyUsages"]?.ToString() ?? "";

                // חילוץ הערך מהטקסט
                return ParseUsageText(monthlyUsages);
            }
            catch
            {
                return double.NaN;
            }
        }

        private static double ParseUsageText(string text)
        {
            // דוגמה: "נפח השימוש המדווח על ידי 'פרטנר': 15.285Gb."
            var match = System.Text.RegularExpressions.Regex.Match(text, @"([\d.]+)\s*([KMGT]?[Bb])");
            if (match.Success)
            {
                double value = double.Parse(match.Groups[1].Value);
                string unit = match.Groups[2].Value.ToUpper();

                double factor = unit switch
                {
                    "KB" => 1.0 / (1024 * 1024),
                    "MB" => 1.0 / 1024,
                    "GB" => 1.0,
                    "TB" => 1024.0,
                    _ => 1.0
                };

                return value * factor;
            }
            return double.NaN;
        }
    }
}
