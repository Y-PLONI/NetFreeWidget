using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Windows.Widgets.Providers;
using Microsoft.Windows.Widgets;
using NetFreeWidget.Core;
using NetFreeWidget.Core.Models;

namespace NetFreeBoardWidgetProvider
{
    internal class WidgetProvider : IWidgetProvider
    {
        public const string WidgetProviderClassId = "E7B3C2A1-4F5D-4A8B-9C3E-1D2F3A4B5C6D";

        // Accessed from concurrent COM threads.
        private static readonly ConcurrentDictionary<string, WidgetSize> ActiveWidgets = new();

        /// <summary>Picks up widgets that already exist when Windows relaunches the provider (runs on first activation).</summary>
        static WidgetProvider()
        {
            try
            {
                foreach (var info in WidgetManager.GetDefault().GetWidgetInfos())
                    ActiveWidgets[info.WidgetContext.Id] = info.WidgetContext.Size;
            }
            catch (Exception ex)
            {
                Log.Error("WidgetProvider", ex);
            }
        }

        public void CreateWidget(WidgetContext widgetContext)
        {
            ActiveWidgets[widgetContext.Id] = widgetContext.Size;
            _ = UpdateWidgetAsync(widgetContext.Id, widgetContext.Size, force: false);
        }

        public void DeleteWidget(string widgetId, string customState)
        {
            ActiveWidgets.TryRemove(widgetId, out _);
            if (ActiveWidgets.IsEmpty)
                Program.ExitEvent.Set();
        }

        public void Activate(WidgetContext widgetContext)
        {
            ActiveWidgets[widgetContext.Id] = widgetContext.Size;
            _ = UpdateWidgetAsync(widgetContext.Id, widgetContext.Size, force: false);
        }

        public void Deactivate(string widgetId)
        {
            // Nothing runs in the background, so there is nothing to pause.
        }

        public void OnActionInvoked(WidgetActionInvokedArgs args)
        {
            if (args.Verb == "refresh")
            {
                _ = UpdateWidgetAsync(args.WidgetContext.Id, args.WidgetContext.Size, force: true);
            }
        }

        public void OnWidgetContextChanged(WidgetContextChangedArgs args)
        {
            ActiveWidgets[args.WidgetContext.Id] = args.WidgetContext.Size;
            _ = UpdateWidgetAsync(args.WidgetContext.Id, args.WidgetContext.Size, force: false);
        }

        private static async Task UpdateWidgetAsync(string widgetId, WidgetSize size, bool force)
        {
            try
            {
                // All widgets share one cached fetch, so a board open or resize usually costs no network.
                var snapshot = await UsageService.GetSnapshotAsync(SettingsStore.Load(), force).ConfigureAwait(false);

                var options = new WidgetUpdateRequestOptions(widgetId)
                {
                    Template = GetTemplateForSize(size),
                    Data = BuildDataJson(snapshot),
                    CustomState = ""
                };

                WidgetManager.GetDefault().UpdateWidget(options);
            }
            catch (Exception ex)
            {
                Log.Error("WidgetProvider", ex);
            }
        }

        private static string GetTemplateForSize(WidgetSize size)
        {
            if (size == WidgetSize.Small)
            {
                return """
                {
                  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                  "type": "AdaptiveCard",
                  "version": "1.5",
                  "body": [
                    {
                      "type": "TextBlock",
                      "text": "נטפרי",
                      "weight": "Bolder",
                      "size": "Medium",
                      "wrap": true
                    },
                    {
                      "type": "TextBlock",
                      "text": "${used} / ${total} GB",
                      "wrap": true,
                      "size": "Small"
                    },
                    {
                      "type": "TextBlock",
                      "text": "${status}",
                      "wrap": true,
                      "color": "${statusColor}",
                      "size": "Small"
                    }
                  ]
                }
                """;
            }
            else if (size == WidgetSize.Large)
            {
                return """
                {
                  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                  "type": "AdaptiveCard",
                  "version": "1.5",
                  "body": [
                    {
                      "type": "TextBlock",
                      "text": "גלישה - נטפרי",
                      "weight": "Bolder",
                      "size": "Large",
                      "wrap": true
                    },
                    {
                      "type": "ColumnSet",
                      "columns": [
                        {
                          "type": "Column",
                          "width": "stretch",
                          "items": [
                            {
                              "type": "TextBlock",
                              "text": "נוצל:",
                              "weight": "Bolder",
                              "wrap": true
                            },
                            {
                              "type": "TextBlock",
                              "text": "${used} GB",
                              "size": "Large",
                              "wrap": true
                            }
                          ]
                        },
                        {
                          "type": "Column",
                          "width": "stretch",
                          "items": [
                            {
                              "type": "TextBlock",
                              "text": "חבילה:",
                              "weight": "Bolder",
                              "wrap": true
                            },
                            {
                              "type": "TextBlock",
                              "text": "${total} GB",
                              "size": "Large",
                              "wrap": true
                            }
                          ]
                        }
                      ]
                    },
                    {
                      "type": "TextBlock",
                      "text": "תקין להיום: ${expected} GB",
                      "wrap": true,
                      "spacing": "Medium"
                    },
                    {
                      "type": "TextBlock",
                      "text": "${status}",
                      "wrap": true,
                      "color": "${statusColor}",
                      "weight": "Bolder",
                      "spacing": "Small"
                    },
                    {
                      "type": "TextBlock",
                      "text": "${exhaustion}",
                      "wrap": true,
                      "isVisible": "${hasExhaustion}",
                      "spacing": "Small"
                    }
                  ],
                  "actions": [
                    {
                      "type": "Action.Execute",
                      "title": "רענן",
                      "verb": "refresh"
                    }
                  ]
                }
                """;
            }
            else
            {
                return """
                {
                  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                  "type": "AdaptiveCard",
                  "version": "1.5",
                  "body": [
                    {
                      "type": "TextBlock",
                      "text": "גלישה - נטפרי",
                      "weight": "Bolder",
                      "size": "Medium",
                      "wrap": true
                    },
                    {
                      "type": "TextBlock",
                      "text": "נוצל: ${used} GB",
                      "wrap": true
                    },
                    {
                      "type": "TextBlock",
                      "text": "חבילה: ${total} GB",
                      "wrap": true
                    },
                    {
                      "type": "TextBlock",
                      "text": "תקין להיום: ${expected} GB",
                      "wrap": true
                    },
                    {
                      "type": "TextBlock",
                      "text": "${status}",
                      "wrap": true,
                      "color": "${statusColor}",
                      "weight": "Bolder"
                    }
                  ],
                  "actions": [
                    {
                      "type": "Action.Execute",
                      "title": "רענן",
                      "verb": "refresh"
                    }
                  ]
                }
                """;
            }
        }

        private static string BuildDataJson(UsageSnapshot snapshot)
        {
            string statusColor = snapshot.IsError ? "Warning" : snapshot.IsOverLimit ? "Attention" : "Good";
            string exhaustion = snapshot.ExhaustionText ?? "";

            // Utf8JsonWriter escapes the strings; invariant formatting keeps "." as the decimal separator.
            using var buffer = new MemoryStream(256);
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("used", snapshot.UsedGb.ToString("F2", CultureInfo.InvariantCulture));
                writer.WriteString("total", snapshot.TotalGb.ToString("F1", CultureInfo.InvariantCulture));
                writer.WriteString("expected", snapshot.ExpectedGb.ToString("F2", CultureInfo.InvariantCulture));
                writer.WriteString("status", snapshot.StatusText);
                writer.WriteString("statusColor", statusColor);
                writer.WriteString("exhaustion", exhaustion);
                writer.WriteBoolean("hasExhaustion", exhaustion.Length > 0);
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
    }
}
