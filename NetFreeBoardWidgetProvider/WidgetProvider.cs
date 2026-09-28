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
    internal class WidgetProvider : IWidgetProvider, IWidgetProvider2
    {
        public const string WidgetProviderClassId = "E7B3C2A1-4F5D-4A8B-9C3E-1D2F3A4B5C6D";

        // Accessed from concurrent COM threads.
        private static readonly ConcurrentDictionary<string, WidgetSize> ActiveWidgets = new();

        // Widgets currently showing the settings form, with the validation error to show (or "").
        private static readonly ConcurrentDictionary<string, string> Customizing = new();

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
            SelfUpdater.CheckInBackground();
            _ = UpdateWidgetAsync(widgetContext.Id, widgetContext.Size, force: false);
        }

        public void DeleteWidget(string widgetId, string customState)
        {
            ActiveWidgets.TryRemove(widgetId, out _);
            Customizing.TryRemove(widgetId, out _);
            if (ActiveWidgets.IsEmpty)
                Program.ExitEvent.Set();
        }

        public void Activate(WidgetContext widgetContext)
        {
            ActiveWidgets[widgetContext.Id] = widgetContext.Size;
            SelfUpdater.CheckInBackground();
            _ = UpdateWidgetAsync(widgetContext.Id, widgetContext.Size, force: false);
        }

        public void Deactivate(string widgetId)
        {
            // Nothing runs in the background, so there is nothing to pause.
        }

        public void OnActionInvoked(WidgetActionInvokedArgs args)
        {
            string id = args.WidgetContext.Id;
            var size = args.WidgetContext.Size;

            switch (args.Verb)
            {
                case "refresh":
                    _ = UpdateWidgetAsync(id, size, force: true);
                    break;

                case "settings":
                    Customizing[id] = "";
                    _ = UpdateWidgetAsync(id, size, force: false);
                    break;

                case "save":
                    string error = TrySaveSettings(args.Data);
                    if (error.Length > 0)
                    {
                        Customizing[id] = error;
                        _ = UpdateWidgetAsync(id, size, force: false);
                        break;
                    }

                    Customizing.TryRemove(id, out _);
                    // Settings are shared, so every widget instance recalculates.
                    foreach (var widget in ActiveWidgets)
                        _ = UpdateWidgetAsync(widget.Key, widget.Value, force: false);
                    break;

                case "cancel":
                    Customizing.TryRemove(id, out _);
                    _ = UpdateWidgetAsync(id, size, force: false);
                    break;
            }
        }

        /// <summary>The board's own "Customize widget" menu item.</summary>
        public void OnCustomizationRequested(WidgetCustomizationRequestedArgs args)
        {
            Customizing[args.WidgetContext.Id] = "";
            _ = UpdateWidgetAsync(args.WidgetContext.Id, args.WidgetContext.Size, force: false);
        }

        /// <summary>Validates and stores the form inputs; returns an error message, or "" on success.</summary>
        private static string TrySaveSettings(string data)
        {
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrEmpty(data) ? "{}" : data);
                var root = doc.RootElement;

                double quota = ReadNumber(root, "quota");
                if (double.IsNaN(quota) || quota <= 0 || quota > 100_000)
                    return "יש להזין נפח חבילה חיובי ב-GB";

                double day = ReadNumber(root, "cycleDay");
                int cycleDay = double.IsNaN(day) ? 0 : (int)day;
                if (cycleDay is < 0 or > 31)
                    cycleDay = 0;

                string weekend = root.TryGetProperty("weekend", out var w) && w.ValueKind == JsonValueKind.String && w.GetString() == "one" ? "one" : "two";

                SettingsStore.Save(new WidgetSettings
                {
                    PackageQuotaGb = quota,
                    CycleDay = cycleDay,
                    WeekendMode = weekend
                });
                return "";
            }
            catch (Exception ex)
            {
                Log.Error("WidgetProvider", ex);
                return "שמירת ההגדרות נכשלה";
            }
        }

        /// <summary>Inputs arrive as numbers or as strings depending on the host version.</summary>
        private static double ReadNumber(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var v))
                return double.NaN;
            if (v.ValueKind == JsonValueKind.Number)
                return v.GetDouble();
            if (v.ValueKind != JsonValueKind.String)
                return double.NaN;
            return double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : double.NaN;
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
                if (Customizing.TryGetValue(widgetId, out string? error))
                {
                    WidgetManager.GetDefault().UpdateWidget(new WidgetUpdateRequestOptions(widgetId)
                    {
                        Template = BuildSettingsForm(SettingsStore.Load(), error, compact: size == WidgetSize.Small),
                        Data = "{}",
                        CustomState = ""
                    });
                    return;
                }

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
                      "type": "ColumnSet",
                      "columns": [
                        {
                          "type": "Column",
                          "width": "auto",
                          "verticalContentAlignment": "Center",
                          "items": [
                            {
                              "type": "Image",
                              "url": "${gauge}",
                              "width": "64px",
                              "altText": "${percentBig}"
                            }
                          ]
                        },
                        {
                          "type": "Column",
                          "width": "stretch",
                          "verticalContentAlignment": "Center",
                          "items": [
                            {
                              "type": "TextBlock",
                              "text": "${percentBig}",
                              "weight": "Bolder",
                              "size": "Medium"
                            },
                            {
                              "type": "TextBlock",
                              "text": "${status}",
                              "wrap": true,
                              "maxLines": 3,
                              "color": "${statusColor}",
                              "size": "Small",
                              "spacing": "None"
                            }
                          ]
                        }
                      ]
                    }
                  ],
                  "actions": [
                    {
                      "type": "Action.Execute",
                      "title": "רענן",
                      "verb": "refresh"
                    },
                    {
                      "type": "Action.Execute",
                      "title": "הגדרות",
                      "verb": "settings"
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
                      "type": "Image",
                      "url": "${gauge}",
                      "width": "220px",
                      "horizontalAlignment": "Center",
                      "altText": "${percentBig}"
                    },
                    {
                      "type": "TextBlock",
                      "text": "${percentBig}",
                      "weight": "Bolder",
                      "size": "ExtraLarge",
                      "horizontalAlignment": "Center",
                      "spacing": "None"
                    },
                    {
                      "type": "TextBlock",
                      "text": "${used} מתוך ${total} GB",
                      "isSubtle": true,
                      "horizontalAlignment": "Center",
                      "spacing": "None"
                    },
                    {
                      "type": "TextBlock",
                      "text": "${status}",
                      "wrap": true,
                      "color": "${statusColor}",
                      "weight": "Bolder",
                      "horizontalAlignment": "Center",
                      "spacing": "Medium"
                    },
                    {
                      "type": "TextBlock",
                      "text": "${exhaustion}",
                      "wrap": true,
                      "isVisible": "${hasExhaustion}",
                      "horizontalAlignment": "Center",
                      "spacing": "Small"
                    },
                    {
                      "type": "TextBlock",
                      "text": "תקין להיום: ${expected} GB (הסימון הכחול)",
                      "wrap": true,
                      "isVisible": "${hasExpected}",
                      "horizontalAlignment": "Center",
                      "spacing": "Medium"
                    },
                    {
                      "type": "TextBlock",
                      "text": "${percent}",
                      "wrap": true,
                      "isVisible": "${hasPercent}",
                      "horizontalAlignment": "Center",
                      "spacing": "Small"
                    },
                    {
                      "type": "TextBlock",
                      "text": "${cycle}",
                      "wrap": true,
                      "size": "Small",
                      "isSubtle": true,
                      "isVisible": "${hasCycle}",
                      "horizontalAlignment": "Center",
                      "spacing": "Small"
                    }
                  ],
                  "actions": [
                    {
                      "type": "Action.Execute",
                      "title": "רענן",
                      "verb": "refresh"
                    },
                    {
                      "type": "Action.Execute",
                      "title": "הגדרות",
                      "verb": "settings"
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
                      "type": "ColumnSet",
                      "columns": [
                        {
                          "type": "Column",
                          "width": "auto",
                          "verticalContentAlignment": "Center",
                          "items": [
                            {
                              "type": "Image",
                              "url": "${gauge}",
                              "width": "130px",
                              "altText": "${percentBig}"
                            }
                          ]
                        },
                        {
                          "type": "Column",
                          "width": "stretch",
                          "verticalContentAlignment": "Center",
                          "items": [
                            {
                              "type": "TextBlock",
                              "text": "${percentBig}",
                              "weight": "Bolder",
                              "size": "ExtraLarge"
                            },
                            {
                              "type": "TextBlock",
                              "text": "${used} / ${total} GB",
                              "isSubtle": true,
                              "spacing": "None",
                              "wrap": true
                            }
                          ]
                        }
                      ]
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
                      "text": "תקין להיום: ${expected} GB",
                      "wrap": true,
                      "size": "Small",
                      "isVisible": "${hasExpected}",
                      "spacing": "None"
                    },
                    {
                      "type": "TextBlock",
                      "text": "${cycle}",
                      "wrap": true,
                      "size": "Small",
                      "isSubtle": true,
                      "isVisible": "${hasCycle}",
                      "spacing": "None"
                    }
                  ],
                  "actions": [
                    {
                      "type": "Action.Execute",
                      "title": "רענן",
                      "verb": "refresh"
                    },
                    {
                      "type": "Action.Execute",
                      "title": "הגדרות",
                      "verb": "settings"
                    }
                  ]
                }
                """;
            }
        }

        /// <summary>
        /// The settings form, with the current values filled in (built directly, so no data binding is needed).
        /// <paramref name="compact"/> (Small size): the widget clips what does not fit, so save and cancel come
        /// first and the heading and hint are left out. Clipped inputs still submit their current values.
        /// </summary>
        private static string BuildSettingsForm(WidgetSettings settings, string error, bool compact)
        {
            int cycleDay = settings.EffectiveCycleDay();
            var detected = cycleDay == 0 ? UsageTracker.GetCandidateDays(UsageService.LastUserId) : null;

            using var buffer = new MemoryStream(4096);
            using (var w = new Utf8JsonWriter(buffer))
            {
                w.WriteStartObject();
                w.WriteString("$schema", "http://adaptivecards.io/schemas/adaptive-card.json");
                w.WriteString("type", "AdaptiveCard");
                w.WriteString("version", "1.5");
                w.WriteStartArray("body");

                if (compact)
                {
                    w.WriteStartObject();
                    w.WriteString("type", "ActionSet");
                    WriteFormActions(w);
                    w.WriteEndObject();
                    WriteError(w, error);
                }
                else
                {
                    WriteText(w, "הגדרות החבילה", bolder: true);
                }

                w.WriteStartObject();
                w.WriteString("type", "Input.Number");
                w.WriteString("id", "quota");
                w.WriteString("label", "נפח החבילה (GB)");
                w.WriteString("placeholder", "למשל 100");
                w.WriteNumber("min", 1);
                if (settings.PackageQuotaGb > 0)
                    w.WriteNumber("value", settings.PackageQuotaGb);
                w.WriteBoolean("isRequired", true);
                w.WriteString("errorMessage", "יש להזין נפח חבילה");
                w.WriteEndObject();

                w.WriteStartObject();
                w.WriteString("type", "Input.ChoiceSet");
                w.WriteString("id", "cycleDay");
                w.WriteString("label", "יום האיפוס בחודש");
                w.WriteString("style", "compact");
                w.WriteString("value", cycleDay.ToString(CultureInfo.InvariantCulture));
                w.WriteStartArray("choices");
                WriteChoice(w, "לא ידוע - זיהוי אוטומטי", "0");
                for (int day = 1; day <= 31; day++)
                {
                    string v = day.ToString(CultureInfo.InvariantCulture);
                    WriteChoice(w, v, v);
                }
                w.WriteEndArray();
                w.WriteEndObject();

                if (detected != null && !compact)
                {
                    string hint = detected.Count == 1 ? $"זוהה אוטומטית: {detected[0]} לחודש"
                        : detected.Count is > 1 and <= 10 ? $"ימים אפשריים לפי המעקב: {string.Join(", ", detected)}"
                        : "הווידג'ט יזהה את יום האיפוס כשהצריכה תתאפס";
                    WriteText(w, hint, subtle: true);
                }

                w.WriteStartObject();
                w.WriteString("type", "Input.ChoiceSet");
                w.WriteString("id", "weekend");
                w.WriteString("label", "חישוב סוף השבוע");
                w.WriteString("style", "compact");
                w.WriteString("value", settings.WeekendMode == "one" ? "one" : "two");
                w.WriteStartArray("choices");
                WriteChoice(w, "שישי ושבת כשני ימים", "two");
                WriteChoice(w, "שישי ושבת כיום אחד", "one");
                w.WriteEndArray();
                w.WriteEndObject();

                if (!compact)
                    WriteError(w, error);

                w.WriteEndArray();

                if (!compact)
                    WriteFormActions(w);

                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }

        /// <summary>The "actions" array of the settings form, for the card or for an ActionSet.</summary>
        private static void WriteFormActions(Utf8JsonWriter w)
        {
            w.WriteStartArray("actions");
            WriteAction(w, "שמור", "save", "auto");
            WriteAction(w, "ביטול", "cancel", "none");
            w.WriteEndArray();
        }

        private static void WriteError(Utf8JsonWriter w, string error)
        {
            if (error.Length == 0)
                return;
            w.WriteStartObject();
            w.WriteString("type", "TextBlock");
            w.WriteString("text", error);
            w.WriteString("color", "Attention");
            w.WriteBoolean("wrap", true);
            w.WriteEndObject();
        }

        private static void WriteText(Utf8JsonWriter w, string text, bool bolder = false, bool subtle = false)
        {
            w.WriteStartObject();
            w.WriteString("type", "TextBlock");
            w.WriteString("text", text);
            w.WriteBoolean("wrap", true);
            if (bolder)
                w.WriteString("weight", "Bolder");
            if (subtle)
            {
                w.WriteBoolean("isSubtle", true);
                w.WriteString("size", "Small");
            }
            w.WriteEndObject();
        }

        private static void WriteChoice(Utf8JsonWriter w, string title, string value)
        {
            w.WriteStartObject();
            w.WriteString("title", title);
            w.WriteString("value", value);
            w.WriteEndObject();
        }

        private static void WriteAction(Utf8JsonWriter w, string title, string verb, string associatedInputs)
        {
            w.WriteStartObject();
            w.WriteString("type", "Action.Execute");
            w.WriteString("title", title);
            w.WriteString("verb", verb);
            w.WriteString("associatedInputs", associatedInputs);
            w.WriteEndObject();
        }

        private static string BuildDataJson(UsageSnapshot snapshot)
        {
            string statusColor =
                snapshot.IsError || snapshot.IsUncertain ? "Warning" :
                snapshot.IsPending ? "Default" :
                snapshot.IsOverLimit ? "Attention" : "Good";
            string exhaustion = snapshot.ExhaustionText ?? "";

            // Utf8JsonWriter escapes the strings; invariant formatting keeps "." as the decimal separator.
            using var buffer = new MemoryStream(256);
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("used", snapshot.IsNoPackage ? "—" : snapshot.UsedGb.ToString("F2", CultureInfo.InvariantCulture));
                writer.WriteString("total", snapshot.TotalGb.ToString("F1", CultureInfo.InvariantCulture));
                bool hasUsage = !snapshot.IsError && !snapshot.IsNoPackage && snapshot.TotalGb > 0;
                double quota = snapshot.TotalGb;
                writer.WriteString("gauge", GaugeRenderer.Render(
                    hasUsage ? snapshot.UsedGb / quota : double.NaN,
                    hasUsage ? snapshot.ExpectedLoGb / quota : double.NaN,
                    hasUsage ? snapshot.ExpectedHiGb / quota : double.NaN));
                writer.WriteString("percentBig", hasUsage
                    ? (snapshot.UsedGb / quota * 100).ToString("F0", CultureInfo.InvariantCulture) + "%"
                    : "—");
                writer.WriteString("expected", snapshot.ExpectedText);
                writer.WriteBoolean("hasExpected", snapshot.ExpectedText.Length > 0);
                writer.WriteString("percent", snapshot.PercentText);
                writer.WriteBoolean("hasPercent", snapshot.PercentText.Length > 0);
                writer.WriteString("cycle", snapshot.CycleText);
                writer.WriteBoolean("hasCycle", snapshot.CycleText.Length > 0);
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
