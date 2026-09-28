using System;
using System.Collections.Generic;
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
        
        private static readonly Dictionary<string, string> ActiveWidgets = new();

        public void CreateWidget(WidgetContext widgetContext)
        {
            ActiveWidgets[widgetContext.Id] = widgetContext.DefinitionId;
            _ = UpdateWidgetAsync(widgetContext.Id, widgetContext.DefinitionId, WidgetSize.Medium);
        }

        public void DeleteWidget(string widgetId, string customState)
        {
            ActiveWidgets.Remove(widgetId);
        }

        public void Activate(WidgetContext widgetContext)
        {
            var size = widgetContext.Size;
            _ = UpdateWidgetAsync(widgetContext.Id, widgetContext.DefinitionId, size);
        }

        public void Deactivate(string widgetId)
        {
        }

        public void OnActionInvoked(WidgetActionInvokedArgs args)
        {
            if (args.Verb == "refresh")
            {
                _ = UpdateWidgetAsync(args.WidgetContext.Id, args.WidgetContext.DefinitionId, args.WidgetContext.Size);
            }
        }

        public void OnWidgetContextChanged(WidgetContextChangedArgs args)
        {
            _ = UpdateWidgetAsync(args.WidgetContext.Id, args.WidgetContext.DefinitionId, args.WidgetContext.Size);
        }

        private async Task UpdateWidgetAsync(string widgetId, string definitionId, WidgetSize size)
        {
            try
            {
                var settings = SettingsStore.Load();
                var snapshot = await UsageService.GetSnapshotAsync(settings);

                string template = GetTemplateForSize(size);
                string data = BuildDataJson(snapshot);

                var options = new WidgetUpdateRequestOptions(widgetId)
                {
                    Template = template,
                    Data = data,
                    CustomState = ""
                };

                WidgetManager.GetDefault().UpdateWidget(options);
            }
            catch (Exception ex)
            {
                try 
                {
                    string logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetFreeWidget", "error_log.txt");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] WidgetProvider Error:\n{ex}\n");
                } 
                catch {}
            }
        }

        private string GetTemplateForSize(WidgetSize size)
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

        private string BuildDataJson(UsageSnapshot snapshot)
        {
            string statusColor = snapshot.IsOverLimit ? "Attention" : "Good";
            string exhaustion = snapshot.ExhaustionText ?? "";
            bool hasExhaustion = !string.IsNullOrEmpty(exhaustion);

            return $$"""
            {
              "used": "{{snapshot.UsedGb:F2}}",
              "total": "{{snapshot.TotalGb:F1}}",
              "expected": "{{snapshot.ExpectedGb:F2}}",
              "status": "{{snapshot.StatusText}}",
              "statusColor": "{{statusColor}}",
              "exhaustion": "{{exhaustion}}",
              "hasExhaustion": {{hasExhaustion.ToString().ToLower()}}
            }
            """;
        }
    }
}
