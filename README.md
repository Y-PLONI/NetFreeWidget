# NetFree Widget - ווידג'ט מעקב גלישה לנטפרי

ווידג'ט ללוח הווידג'טים של Windows 11 למעקב אחר נפח הגלישה בנטפרי.

## תכונות

- 📊 נפח שנוצל מול גודל החבילה
- 📈 חישוב חריגה יומית חכם (כולל מצב סופ"ש של יום אחד או שניים)
- 🔮 תחזית סיום חבילה
- 📏 3 גדלים: Small, Medium, Large
- 🔄 כפתור רענן
- 🪶 צריכת משאבים נמוכה: אין עבודה ברקע, בקשת רשת אחת משותפת עם מטמון, והתהליך נסגר כשאין ווידג'טים

## מבנה הפרויקט

```
NetFreeWidget/
├── NetFreeWidget.Core/               # לוגיקה (API, חישובים, מודלים)
├── NetFreeBoardWidgetProvider/       # COM server ו-IWidgetProvider
└── NetFreeBoardWidgetPackage/        # אריזת MSIX ונכסים
```

## דרישות מערכת

- Windows 11
- Visual Studio 2022 עם רכיב Windows App SDK / MSIX Packaging (לפיתוח)
- Developer Mode (לבדיקות מקומיות)

## בנייה והתקנה

ראה [QUICK_START.md](QUICK_START.md) להתחלה מהירה, או [WIDGET_SETUP_GUIDE.md](WIDGET_SETUP_GUIDE.md) למדריך מלא.

```powershell
# בנייה (Release כברירת מחדל) והתקנה
.\build-and-deploy.ps1 -Platform x64

# או ב-Visual Studio:
# 1. פתח NetFreeWidget.sln
# 2. Set Startup Project: NetFreeBoardWidgetPackage
# 3. Build + Deploy
```

> שים לב: `dotnet build` לא מספיק. יש לבנות עם MSBuild של Visual Studio (הסקריפט עושה זאת).

## הגדרות

הווידג'ט קורא את ההגדרות מהקובץ:
```
%APPDATA%\NetFreeWidget\settings.json
```

```json
{
  "PackageStartDate": "2024-01-15",
  "PackageQuotaGb": 100.0,
  "WeekendMode": "two"
}
```

- `PackageStartDate` - תאריך התחלת החבילה (yyyy-MM-dd)
- `PackageQuotaGb` - גודל החבילה ב-GB
- `WeekendMode` - `"two"` (שני ימי סופ"ש) או `"one"` (שישי ושבת נספרים כיום אחד)

שינוי בקובץ נקלט בעדכון הבא של הווידג'ט.

## עדכון נתונים

- הנתונים מתעדכנים בכל פתיחה של לוח הווידג'טים, ונשמרים במטמון ל-10 דקות.
- כפתור "רענן" עוקף את המטמון (עם מרווח מינימלי של 10 שניות).
- כשאין חיבור, הניסיונות מתרווחים בהדרגה (30 שניות עד 15 דקות).

## טכנולוגיות

- C# + .NET 8, Adaptive Cards
- Windows App SDK (Widgets), אריזת MSIX
- System.Text.Json

## פיתוח

- לוגיקה וחישובים → `NetFreeWidget.Core`
- תצוגת הכרטיסים (Adaptive Cards) → `NetFreeBoardWidgetProvider/WidgetProvider.cs`
- פתרון בעיות → [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

> התוכנה השולחנית (WPF) הקודמת נמצאת בענף `master`.

## רישיון

MIT License

## קרדיטים

פותח על ידי הקהילה עבור משתמשי נטפרי 💙
