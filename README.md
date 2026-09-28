# NetFree Widget - ווידג'ט מעקב גלישה לנטפרי

אפליקציית Windows למעקב אחר נפח הגלישה בנטפרי - עם תמיכה בווידג'ט שולחני ובלוח הווידג'טים של Windows 11!

## תכונות

### ווידג'ט שולחני
- 📊 מעקב בזמן אמת אחר נפח הגלישה
- 📈 חישוב חריגה יומית חכם
- 🔮 תחזית סיום חבילה
- ⚙️ הגדרות מותאמות אישית
- 🎨 עיצוב Material Design נקי
- 🌙 תמיכה במצב כהה אוטומטי
- 🪟 קצוות מעוגלים בסגנון Windows 11
- 👻 לא מופיע בשורת המשימות (ווידג'ט אמיתי!)
- 🖱️ ניתן לגרירה
- 💾 משקל קל (פחות מ-2MB)

### ווידג'ט ללוח הווידג'טים (Windows 11)
- 📱 אינטגרציה מלאה עם Windows 11 Widgets Board
- 📏 תמיכה ב-3 גדלים: Small, Medium, Large
- 🔄 רענון אוטומטי
- 🎯 Adaptive Cards עם תמיכה ב-Dark/Light Mode
- 📦 התקנה מ-Microsoft Store (בקרוב)

## מבנה הפרויקט

```
NetFreeWidget/
├── NetFreeWidget/                    # ווידג'ט שולחני WPF
├── NetFreeWidget.Core/               # לוגיקה משותפת (API, חישובים)
├── NetFreeBoardWidgetProvider/       # Provider ללוח הווידג'טים
└── NetFreeBoardWidgetPackage/        # אריזת MSIX להתקנה מהחנות
```

## דרישות מערכת

### לווידג'ט שולחני:
- Windows 10/11
- .NET 8.0 Runtime

### לווידג'ט בלוח:
- Windows 11 (Build 19041+)
- Visual Studio 2022 (לפיתוח)
- Developer Mode (לבדיקות מקומיות)

## התקנה והרצה

### ווידג'ט שולחני

#### אופציה 1: הרצה עם .NET Runtime (קל ביותר)
1. התקן .NET 8.0 Runtime מ-[Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0)
2. בנה את הפרויקט:
```bash
dotnet publish -c Release
```
3. הקובץ יימצא ב-`bin/Release/net8.0-windows/publish/NetFreeWidget.exe`

#### אופציה 2: קובץ עצמאי (Self-Contained)
לקובץ EXE שעובד ללא התקנת .NET:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

### ווידג'ט בלוח הווידג'טים

#### התחלה מהירה
ראה [QUICK_START.md](QUICK_START.md) להתחלה מהירה.

#### מדריך מפורט
ראה [WIDGET_SETUP_GUIDE.md](WIDGET_SETUP_GUIDE.md) למדריך מלא.

#### בנייה והתקנה
```powershell
# שימוש בסקריפט אוטומטי
.\build-and-deploy.ps1 -Configuration Debug -Platform x64

# או ב-Visual Studio:
# 1. פתח NetFreeWidget.sln
# 2. Set Startup Project: NetFreeBoardWidgetPackage
# 3. Build + Deploy
```

## שימוש

### הגדרות ראשוניות
1. הרץ את הווידג'ט (שולחני או בלוח)
2. לחץ על ⚙️ להגדרות (בווידג'ט השולחני)
3. הזן:
   - תאריך התחלת החבילה (dd/MM/yyyy)
   - גודל החבילה ב-GB
   - בחר מצב חישוב סופ"ש (יום אחד / שני ימים)
4. לחץ "שמור"

### שיתוף הגדרות
שני הווידג'טים (שולחני + לוח) משתמשים באותו קובץ הגדרות:
```
%APPDATA%\NetFreeWidget\settings.json
```

הווידג'ט יתעדכן אוטומטית כל 5 דקות.

## טכנולוגיות

- **Frontend**: WPF (ווידג'ט שולחני), Adaptive Cards (ווידג'ט בלוח)
- **Backend**: C# + .NET 8.0
- **API**: NetFree API
- **Packaging**: MSIX (Windows App SDK)
- **Dependencies**: Newtonsoft.Json, Microsoft.WindowsAppSDK

## פיתוח

### מבנה הקוד
- `NetFreeWidget.Core` - לוגיקה משותפת (NetFreeApi, UsageCalculator, UsageService)
- `NetFreeWidget` - ווידג'ט שולחני WPF
- `NetFreeBoardWidgetProvider` - COM server ו-IWidgetProvider
- `NetFreeBoardWidgetPackage` - MSIX manifest ונכסים

### הוספת תכונות
1. לוגיקה משותפת → `NetFreeWidget.Core`
2. UI שולחני → `NetFreeWidget/MainWindow.xaml`
3. UI לוח → `NetFreeBoardWidgetProvider/WidgetProvider.cs` (Adaptive Cards)

## רישיון

MIT License

## תרומה

Pull requests מתקבלים בברכה! אנא פתח issue קודם לדיון על שינויים גדולים.

## קרדיטים

פותח על ידי הקהילה עבור משתמשי נטפרי 💙
