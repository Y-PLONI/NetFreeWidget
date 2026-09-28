# פתרון בעיות - NetFree Widget

## בעיות נפוצות ופתרונות

### הווידג'ט לא מופיע בלוח הווידג'טים

#### 1. Developer Mode לא מופעל
**תסמינים**: לא ניתן ל-Deploy את החבילה

**פתרון**:
1. Settings → Privacy & Security → For developers
2. הפעל "Developer Mode"
3. אשר את ההתראה
4. נסה שוב Deploy

#### 2. שגיאת COM Registration
**תסמינים**: הווידג'ט מופיע אבל לא נטען

**פתרון**:
1. ודא שה-GUID זהה בכל המקומות:
   - `NetFreeBoardWidgetProvider/WidgetProviderFactory.cs` (שורה 9)
   - `NetFreeBoardWidgetProvider/WidgetProvider.cs` (שורה 13)
   - `NetFreeBoardWidgetPackage/Package.appxmanifest` (שורות 42, 52)

2. הסר את החבילה הקיימת:
   ```powershell
   Get-AppxPackage *NetFree* | Remove-AppxPackage
   ```

3. בנה מחדש ו-Deploy שוב

#### 3. חבילה לא נמצאת
**תסמינים**: "Package not found" בעת Deploy

**פתרון**:
1. ודא שבחרת את הפלטפורמה הנכונה (x64/x86/ARM64)
2. בצע Clean Solution
3. Rebuild All
4. Deploy שוב

### הווידג'ט מציג "שגיאה בחיבור"

#### 1. בעיית רשת
**פתרון**:
- ודא שיש חיבור לאינטרנט
- נסה לגשת ל-https://netfree.link בדפדפן
- בדוק firewall/antivirus

#### 2. API של NetFree לא זמין
**פתרון**:
- המתן מספר דקות ונסה שוב
- לחץ על כפתור "רענן"

### הווידג'ט מציג "חסרים נתונים להגדרה"

**סיבה**: לא הוגדרו תאריך התחלה או נפח חבילה

**פתרון**:

1. צור/ערוך את הקובץ:
   ```
   %APPDATA%\NetFreeWidget\settings.json
   ```

2. הוסף:
   ```json
   {
     "PackageStartDate": "2024-01-15",
     "PackageQuotaGb": 100.0,
     "WeekendMode": "two"
   }
   ```

3. שמור ורענן את הווידג'ט

### שגיאות Build

#### 1. "Microsoft.WindowsAppSDK not found"
**פתרון**:
```powershell
dotnet restore NetFreeWidget.sln
```

#### 2. "Target platform not supported"
**פתרון**:
1. ודא שמותקן Windows SDK 10.0.19041.0 ומעלה
2. Visual Studio Installer → Modify → Individual Components
3. חפש "Windows 10 SDK" והתקן

#### 3. "C++ build tools required"
**פתרון**:
1. Visual Studio Installer → Modify
2. Individual Components
3. התקן "MSVC v143 - VS 2022 C++ x64/x86 build tools"

### בעיות ניפוי שגיאות (Debugging)

#### לא ניתן ל-Attach לתהליך
**פתרון**:
1. הוסף את הווידג'ט ללוח קודם
2. Debug → Attach to Process
3. חפש "NetFreeBoardWidgetProvider.exe"
4. אם לא נמצא, בדוק ב-Task Manager

#### Breakpoints לא עובדים
**פתרון**:
1. ודא שבנית ב-Debug mode (לא Release)
2. Project Properties → Build → Optimize code = false
3. Rebuild

### הווידג'ט לא מתעדכן

#### 1. רענון אוטומטי לא עובד
**פתרון**:
- הווידג'ט מתעדכן רק כש-Widgets Board פתוח
- לחץ על כפתור "רענן" לעדכון ידני

#### 2. נתונים ישנים
**פתרון**:
1. סגור את Widgets Board (WIN + W)
2. פתח שוב
3. הווידג'ט יתעדכן אוטומטית

### בעיות עם נכסי עיצוב

#### תמונות לא מופיעות
**פתרון**:
1. ודא שכל התמונות קיימות ב-`NetFreeBoardWidgetPackage/ProviderAssets/`
2. ודא שהשמות תואמים ל-manifest:
   - StoreLogo.png
   - Square44x44Logo.png
   - Square150x150Logo.png
   - Wide310x150Logo.png
   - WidgetIcon.png
   - WidgetPreview.png

3. Rebuild הפרויקט

#### תמונות מעוותות
**פתרון**:
- השתמש בתמונות PNG עם רקע שקוף
- שמור על יחס גובה-רוחב נכון
- השתמש ב-ffmpeg ליצירה מחדש:
  ```powershell
  ffmpeg -i logo.png -vf "scale=300:304:force_original_aspect_ratio=decrease,pad=300:304:(ow-iw)/2:(oh-ih)/2:color=0x00000000" WidgetPreview.png
  ```

## לוגים ומידע נוסף

### Event Viewer
לבדיקת שגיאות מערכת:
1. WIN + X → Event Viewer
2. Windows Logs → Application
3. חפש אירועים מ-"NetFreeBoardWidgetProvider"

### קבצי לוג
הווידג'ט לא יוצר קבצי לוג כרגע. לצורך ניפוי שגיאות, השתמש ב-Debug mode ב-Visual Studio.

### מידע על החבילה
```powershell
# רשימת חבילות מותקנות
Get-AppxPackage *NetFree*

# מידע מפורט
Get-AppxPackage *NetFree* | Format-List

# הסרת חבילה
Get-AppxPackage *NetFree* | Remove-AppxPackage
```

## קבלת עזרה

אם הבעיה נמשכת:

1. בדוק את [Issues](https://github.com/your-repo/issues) ב-GitHub
2. פתח Issue חדש עם:
   - תיאור הבעיה
   - צילומי מסך
   - לוגים מ-Event Viewer
   - גרסת Windows
   - גרסת Visual Studio

## טיפים למניעת בעיות

1. **תמיד בנה ב-Clean Solution** לפני Deploy
2. **השתמש ב-x64** אלא אם יש סיבה ספציפית אחרת
3. **עדכן Windows App SDK** לגרסה האחרונה
4. **גבה את settings.json** לפני שינויים גדולים
5. **השתמש ב-Git** לניהול גרסאות

---

עדיין תקוע? פתח Issue ונעזור לך! 🚀
