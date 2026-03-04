# NetFree Widget - ווידג'ט מעקב גלישה לנטפרי

אפליקציית Windows קטנה ומינימליסטית למעקב אחר נפח הגלישה בנטפרי - ווידג'ט אמיתי שיושב על שולחן העבודה!

## תכונות

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

## דרישות מערכת

- Windows 10/11
- .NET 8.0 Runtime

## התקנה

### אופציה 1: הרצה עם .NET Runtime (קל ביותר)

1. התקן .NET 8.0 Runtime מ-[Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0)
2. בנה את הפרויקט:
```bash
dotnet publish -c Release
```
3. הקובץ יימצא ב-`bin/Release/net8.0-windows/publish/NetFreeWidget.exe`

### אופציה 2: קובץ עצמאי (Self-Contained)

לקובץ EXE שעובד ללא התקנת .NET:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

הקובץ יהיה כ-20MB אבל יעבוד על כל מחשב Windows.

## שימוש

1. הרץ את `NetFreeWidget.exe`
2. לחץ על ⚙️ להגדרות
3. הזן:
   - תאריך התחלת החבילה
   - גודל החבילה ב-GB
   - בחר מצב חישוב סופ"ש
4. לחץ "שמור"

הווידג'ט יתעדכן אוטומטית כל 5 דקות.

## הגדרות

ההגדרות נשמרות ב:
```
%APPDATA%\NetFreeWidget\settings.json
```

## טכנולוגיות

- C# + WinForms
- .NET 8.0
- Newtonsoft.Json

## רישיון

MIT
