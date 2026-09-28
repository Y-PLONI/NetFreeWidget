# מדריך הגדרה - ווידג'ט נטפרי ללוח הווידג'טים של Windows 11

## מבנה הפרויקט

```
NetFreeWidget.sln
├── NetFreeWidget.Core/               # לוגיקה (API, חישובים, מודלים)
│   ├── Models/
│   │   ├── UsageSnapshot.cs
│   │   └── WidgetSettings.cs
│   ├── NetFreeApi.cs
│   ├── UsageCalculator.cs
│   └── UsageService.cs
├── NetFreeBoardWidgetProvider/       # Provider ללוח הווידג'טים
│   ├── Program.cs
│   ├── WidgetProvider.cs
│   ├── WidgetProviderFactory.cs
│   └── SettingsStore.cs
└── NetFreeBoardWidgetPackage/        # אריזת MSIX
    ├── Package.appxmanifest
    └── ProviderAssets/
```

## שלב 1: הכנת סביבת הפיתוח

### דרישות מקדימות:
1. Windows 11 (Build 19041 ומעלה)
2. Visual Studio 2022 או חדש יותר
3. Workloads נדרשים:
   - .NET Desktop Development
   - Universal Windows Platform development
   - C++ (v143) build tools

### הפעלת Developer Mode:
1. Settings → Privacy & Security → For developers
2. הפעל "Developer Mode"

## שלב 2: יצירת נכסי עיצוב

צור את התמונות הבאות בתיקייה `NetFreeBoardWidgetPackage/ProviderAssets/`:

### תמונות נדרשות:
- **StoreLogo.png** - 50×50 פיקסלים
- **Square44x44Logo.png** - 44×44 פיקסלים
- **Square150x150Logo.png** - 150×150 פיקסלים
- **Wide310x150Logo.png** - 310×150 פיקסלים
- **WidgetIcon.png** - 64×64 פיקסלים
- **WidgetPreview.png** - 300×304 פיקסלים (עם פינות מעוגלות שקופות)

### טיפים לעיצוב:
- השתמש ברקע שקוף (PNG)
- צבעים שמתאימים גם ל-Light וגם ל-Dark mode
- WidgetPreview צריך להראות את הווידג'ט בגודל Medium

## שלב 3: בנייה ובדיקה מקומית

### פתיחת הפרויקט:
```bash
# פתח את NetFreeWidget.sln ב-Visual Studio
```

### בנייה:
1. בחר את הפלטפורמה: x64 (או x86/ARM64)
2. בחר Configuration: Debug
3. Set Startup Project → NetFreeBoardWidgetPackage
4. Build → Build Solution (Ctrl+Shift+B)

### Deploy מקומי:
1. לחץ ימני על NetFreeBoardWidgetPackage
2. בחר "Deploy"
3. המתן לסיום ההתקנה

### בדיקת הווידג'ט:
1. לחץ WIN + W (פתיחת לוח הווידג'טים)
2. לחץ על + ליד האווטאר
3. חפש "גלישה - נטפרי"
4. הוסף את הווידג'ט

## שלב 4: ניפוי שגיאות (Debugging)

### אופציה 1: Attach to Process
1. הוסף את הווידג'ט ללוח
2. Debug → Attach to Process
3. חפש "NetFreeBoardWidgetProvider.exe"
4. לחץ Attach

### אופציה 2: Debug Installed App Package
1. Debug → Other Debug Targets → Debug Installed App Package
2. בחר את החבילה "NetFreeWidget"
3. סמן "Do not launch, but debug my code when it starts"
4. לחץ Start
5. הוסף את הווידג'ט ללוח

## שלב 5: הגדרות ראשוניות

הווידג'ט קורא את ההגדרות מהקובץ:
```
%APPDATA%\NetFreeWidget\settings.json
```

אם אין קובץ הגדרות, צור אותו ידנית:
```json
{
  "PackageStartDate": "2024-01-15",
  "PackageQuotaGb": 100.0,
  "WeekendMode": "two"
}
```

## שלב 6: הכנה לפרסום

### עדכון OutputType:
לפני פרסום, שנה את `NetFreeBoardWidgetProvider.csproj`:
```xml
<OutputType>WinExe</OutputType>  <!-- במקום Exe -->
```
זה ימנע הצגת חלון קונסול למשתמשים.

### עדכון Package.appxmanifest:
1. עדכן את `Publisher` לפי התעודה שלך
2. עדכן את `PublisherDisplayName` לשם שלך
3. עדכן את `Version` לפי הצורך

## שלב 7: יצירת חבילה לחנות

### יצירת Package:
1. לחץ ימני על NetFreeBoardWidgetPackage
2. Publish → Create App Packages
3. בחר "Microsoft Store"
4. התחבר לחשבון Partner Center
5. בחר את האפליקציה או צור חדשה
6. בחר פלטפורמות: x64, x86, ARM64
7. לחץ Create

### העלאה ל-Partner Center:
1. היכנס ל-[Partner Center](https://partner.microsoft.com/dashboard)
2. צור מוצר חדש
3. מלא את כל השדות הנדרשים:
   - Pricing and availability
   - Properties
   - Age ratings
   - Packages (העלה את ה-MSIX)
   - Store listings (תיאור, screenshots)
4. הגש ל-Certification

## שלב 8: Screenshots לחנות

צור לפחות 4 screenshots:
- 1920×1080 או 1366×768
- הצג את הווידג'ט בגדלים שונים
- הצג גם Light וגם Dark mode
- הוסף טקסט הסבר בעברית

## טיפים נוספים

### עדכון אוטומטי:
הווידג'ט מתעדכן בכל פתיחה של לוח הווידג'טים. הנתונים נשמרים במטמון ל-10 דקות (ניתן לשנות ב-`NetFreeWidget.Core/UsageService.cs`), וכפתור "רענן" עוקף את המטמון.

### תמיכה בגדלים:
הווידג'ט תומך ב-3 גדלים: Small, Medium, Large. כל גודל מציג מידע שונה.

### בדיקת Adaptive Cards:
השתמש ב-[Adaptive Cards Designer](https://adaptivecards.io/designer/) לבדיקת העיצוב.

## פתרון בעיות נפוצות

### הווידג'ט לא מופיע בלוח:
- ודא ש-Developer Mode מופעל
- ודא שה-GUID זהה בכל המקומות
- בדוק Event Viewer → Windows Logs → Application

### שגיאת COM:
- ודא שה-ClassId ב-manifest תואם ל-GUID ב-WidgetProviderFactory.cs
- ודא שה-COM server רשום נכון

### הווידג'ט לא מתעדכן:
- בדוק חיבור לאינטרנט
- בדוק שקובץ ההגדרות קיים ותקין
- הוסף breakpoints ב-UpdateWidgetAsync

## קישורים שימושיים

- [Windows Widgets Documentation](https://learn.microsoft.com/windows/apps/design/widgets/)
- [Adaptive Cards Designer](https://adaptivecards.io/designer/)
- [Partner Center](https://partner.microsoft.com/dashboard)
- [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/)

---

בהצלחה! 🎉
