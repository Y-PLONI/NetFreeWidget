using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NetFreeWidget.Core
{
    public class NetFreeApi
    {
        private static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public static async Task<double> GetUsageGb()
        {
            try
            {
                string userUrl = $"https://user-info.internal.netfree.link/user/{new Random().NextDouble()}";
                string userResponse = await client.GetStringAsync(userUrl);
                var userObj = JObject.Parse(userResponse);
                string enc = userObj["enc"]?.ToString() ?? "";

                if (string.IsNullOrEmpty(enc))
                    throw new Exception("Failed to get enc");

                var payload = new { enc = enc };
                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                string linkInfoUrl = "https://netfree.link/api/user/link-info";
                var response = await client.PostAsync(linkInfoUrl, content);
                string linkInfoResponse = await response.Content.ReadAsStringAsync();
                
                var linkInfo = JObject.Parse(linkInfoResponse);
                string monthlyUsages = linkInfo["monthlyUsages"]?.ToString() ?? "";

                return ParseUsageText(monthlyUsages);
            }
            catch (Exception ex)
            {
                try 
                {
                    string logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetFreeWidget", "error_log.txt");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] NetFreeApi Error: {ex}\n");
                } 
                catch {}
                
                return double.NaN;
            }
        }

        private static double ParseUsageText(string text)
        {
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
