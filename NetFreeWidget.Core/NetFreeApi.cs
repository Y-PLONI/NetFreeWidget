using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NetFreeWidget.Core
{
    public static partial class NetFreeApi
    {
        // Single pooled connection that is released shortly after use, so no sockets stay open between refreshes.
        private static readonly HttpClient client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 1
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*([KMGT]?B)", RegexOptions.IgnoreCase)]
        private static partial Regex UsageRegex();

        public static async Task<double> GetUsageGb()
        {
            try
            {
                string userUrl = $"https://user-info.internal.netfree.link/user/{Random.Shared.NextDouble()}";
                string enc;
                using (var userResponse = await client.GetAsync(userUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    userResponse.EnsureSuccessStatusCode();
                    await using var stream = await userResponse.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(stream);
                    enc = doc.RootElement.TryGetProperty("enc", out var e) ? e.GetString() ?? "" : "";
                }

                if (string.IsNullOrEmpty(enc))
                {
                    Log.Error("NetFreeApi", "Missing enc in user-info response");
                    return double.NaN;
                }

                using var content = new StringContent("{\"enc\":" + JsonSerializer.Serialize(enc) + "}", Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("https://netfree.link/api/user/link-info", content);
                response.EnsureSuccessStatusCode();
                await using var linkStream = await response.Content.ReadAsStreamAsync();
                using var linkDoc = await JsonDocument.ParseAsync(linkStream);
                string monthlyUsages = linkDoc.RootElement.TryGetProperty("monthlyUsages", out var m) ? m.ToString() : "";

                return ParseUsageText(monthlyUsages);
            }
            catch (Exception ex)
            {
                Log.Error("NetFreeApi", ex);
                return double.NaN;
            }
        }

        private static double ParseUsageText(string text)
        {
            var match = UsageRegex().Match(text);
            if (!match.Success)
                return double.NaN;

            double value = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            double factor = match.Groups[2].Value.ToUpperInvariant() switch
            {
                "KB" => 1.0 / (1024 * 1024),
                "MB" => 1.0 / 1024,
                "TB" => 1024.0,
                _ => 1.0
            };

            return value * factor;
        }
    }
}
