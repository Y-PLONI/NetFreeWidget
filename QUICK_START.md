# התחלה מהירה - ווידג'ט נטפרי ללוח הווידג'טים

## מה נוצר?

יצרתי עבורך 3 פרויקטים חדשים:

1. **NetFreeWidget.Core** - לוגיקה משותפת (API, חישובים, מודלים)
2. **NetFreeBoardWidgetProvider** - ה-Provider ללוח הווידג'טים של Windows 11
3. **NetFreeBoardWidgetPackage** - אריזת MSIX להתקנה מהחנות

הפרויקט המקורי שלך (NetFreeWidget) נשאר בדיוק כמו שהיה!

## צעדים הבאים

### 1. ✅ נכסי עיצוב - מוכן!
כל התמונות הנדרשות כבר נוצרו אוטומטית מה-logo.png שלך:
- ✅ StoreLogo.png (50×50)
- ✅ Square44x44Logo.png (44×44)
- ✅ Square150x150Logo.png (150×150)
- ✅ Wide310x150Logo.png (310×150)
- ✅ WidgetIcon.png (64×64)
- ✅ WidgetPreview.png (300×304)

### 2. פתח ב-Visual Studio 2022
```bash
# פתח את הקובץ:
NetFreeWidget.sln
```

### 3. התקן Workloads נדרשים
אם עדיין לא מותקנים:
- .NET Desktop Development
- Universal Windows Platform development
- C++ (v143) build tools

### 4. הפעל Developer Mode
Settings → Privacy & Security → For developers → Developer Mode

### 5. בנה ו-Deploy
1. בחר Platform: x64
2. Set Startup Project: NetFreeBoardWidgetPackage
3. Build Solution (Ctrl+Shift+B)
4. לחץ ימני על NetFreeBoardWidgetPackage → Deploy

### 6. בדוק את הווידג'ט
1. WIN + W (פתיחת לוח הווידג'טים)
2. לחץ על + ליד האווטאר
3. חפש "גלישה - נטפרי"
4. הוסף את הווידג'ט

## מה הווידג'ט עושה?

הווידג'ט מציג:
- **Small**: שימוש בסיסי וסטטוס
- **Medium**: שימוש, חבילה, תקין להיום, סטטוס + כפתור רענן
- **Large**: כל המידע + תחזית גמר חבילה

הווידג'ט משתמש באותו קובץ הגדרות כמו הווידג'ט השולחני:
`%APPDATA%\NetFreeWidget\settings.json`

## מידע טכני חשוב

### GUID משותף
כל הפרויקט משתמש ב-GUID אחד:
```
E7B3C2A1-4F5D-4A8B-9C3E-1D2F3A4B5C6D
```

אם תרצה לשנות אותו, עדכן ב-3 מקומות:
1. `NetFreeBoardWidgetProvider/WidgetProviderFactory.cs` (שורה 9)
2. `NetFreeBoardWidgetProvider/WidgetProvider.cs` (שורה 13)
3. `NetFreeBoardWidgetPackage/Package.appxmanifest` (שורות 42, 52)

### שיתוף הגדרות
שני הווידג'טים (שולחני + לוח) משתמשים באותו קובץ הגדרות, כך שאפשר להגדיר פעם אחת.

## לפני פרסום לחנות

1. עדכן `NetFreeBoardWidgetProvider.csproj`:
   ```xml
   <OutputType>WinExe</OutputType>
   ```

2. עדכן `Package.appxmanifest`:
   - Publisher (לפי התעודה שלך)
   - PublisherDisplayName (השם שלך)
   - Version

3. צור screenshots לחנות (1920×1080 או 1366×768)

4. הירשם ל-[Partner Center](https://partner.microsoft.com/dashboard)

## קבצי עזרה

- **WIDGET_SETUP_GUIDE.md** - מדריך מפורט מלא
- **NetFreeBoardWidgetPackage/ProviderAssets/README.md** - דרישות נכסי עיצוב

## פתרון בעיות

אם הווידג'ט לא מופיע:
1. ודא ש-Developer Mode מופעל
2. בדוק Event Viewer → Windows Logs → Application
3. ודא שכל ה-GUIDs זהים
4. נסה Rebuild All

---

מוכן לעבודה! 🚀
