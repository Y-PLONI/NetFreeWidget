// ==UserScript==
// @name         NetFree Usage Tracker (Global & Flutter Style)
// @namespace    http://tampermonkey.net/
// @version      2.1
// @description  מעקב חכם ומעוצב אחרי נפח הגלישה בנטפרי. כולל לחצן צף, חישובי חריגה ותחזית סיום חבילה.
// @author       AI
// @match        *://*/*
// @grant        GM_setValue
// @grant        GM_getValue
// @grant        GM_addStyle
// @grant        GM_xmlhttpRequest
// @connect      netfree.link
// @connect      user-info.internal.netfree.link
// ==/UserScript==

(function() {
    'use strict';

    // מניעת כפילות של הסקריפט
    if (window.__NF_FLUTTER_WIDGET__) return;
    window.__NF_FLUTTER_WIDGET__ = true;

    // --- הגדרות מערכת ומצב ---
    const STORAGE_KEY = 'nf_flutter_settings_v2';
    const REFRESH_MS = 5 * 60 * 1000; // כל 5 דקות

    const DEFAULT_SETTINGS = {
        packageStartDate: '', // YYYY-MM-DD
        packageQuotaGb: '',
        weekendMode: 'two',   // 'two' = רגיל, 'one' = שישי ושבת כיום אחד
    };

    const state = {
        settings: loadSettings(),
        data: null,
        loading: false,
        error: '',
    };

    // --- עיצוב (Flutter / Material) ---
    GM_addStyle(`
        @import url('https://fonts.googleapis.com/css2?family=Rubik:wght@400;500;700&display=swap');

        #nf-flutter-tracker-wrap * {
            box-sizing: border-box;
            font-family: 'Rubik', sans-serif;
        }

        #nf-flutter-tracker-wrap {
            position: fixed;
            bottom: 20px;
            left: 20px;
            z-index: 2147483647;
            direction: rtl;
        }

        /* הכפתור הצף המוקטן */
        #nf-flutter-tracker-fab {
            width: 44px;
            height: 44px;
            background-color: rgba(33, 150, 243, 0.65);
            border-radius: 50%;
            box-shadow: 0 4px 10px rgba(0,0,0,0.15);
            display: flex;
            justify-content: center;
            align-items: center;
            cursor: pointer;
            transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
            backdrop-filter: blur(5px);
            margin-top: 15px;
        }
        #nf-flutter-tracker-fab:hover {
            background-color: rgba(33, 150, 243, 1);
            box-shadow: 0 6px 14px rgba(0,0,0,0.25);
            transform: scale(1.05);
        }
        #nf-flutter-tracker-fab svg {
            fill: white;
            width: 20px;
            height: 20px;
        }

        /* חלונית הפופאפ */
        #nf-flutter-tracker-popup {
            position: absolute;
            bottom: 60px;
            left: 0;
            width: 320px;
            background: #ffffff;
            border-radius: 16px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.15);
            opacity: 0;
            visibility: hidden;
            transform: translateY(15px);
            transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
            overflow: hidden;
            display: flex;
            flex-direction: column;
            color: #212121;
        }
        #nf-flutter-tracker-popup.show {
            opacity: 1;
            visibility: visible;
            transform: translateY(0);
        }

        /* תוכן פנימי */
        .nf-header {
            background: #2196F3;
            color: white;
            padding: 14px 16px;
            font-weight: 500;
            font-size: 15px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .nf-icon-btn {
            background: none; border: none; color: white; cursor: pointer;
            padding: 4px; border-radius: 50%; display: flex; transition: background 0.2s;
        }
        .nf-icon-btn:hover { background: rgba(255,255,255,0.2); }
        .nf-icon-btn svg { fill: white; width: 18px; height: 18px; }

        .nf-body { padding: 16px; }

        .nf-data-row {
            display: flex; justify-content: space-between; margin-bottom: 8px;
            font-size: 13px; color: #616161;
        }
        .nf-data-row strong { color: #212121; font-weight: 500; }

        /* סרגל התקדמות */
        .nf-progress-wrap { position: relative; margin: 15px 0; }
        .nf-progress-bg {
            background: #E0E0E0; border-radius: 8px; height: 8px; width: 100%; overflow: hidden;
        }
        .nf-progress-bar {
            background: #2196F3; height: 100%; border-radius: 8px; transition: width 0.5s ease;
        }
        .nf-progress-bar.danger { background: #F44336; }
        .nf-progress-marker {
            position: absolute; top: -3px; height: 14px; width: 2px;
            background: #424242; border-radius: 2px; transition: right 0.5s ease;
            box-shadow: 0 0 4px rgba(255,255,255,0.8);
        }

        /* התראות וסטטוס */
        .nf-alert {
            background: #E8F5E9; color: #2E7D32; padding: 10px; border-radius: 8px;
            font-size: 12px; margin-top: 10px; display: block; line-height: 1.4;
        }
        .nf-alert.danger { background: #FFEBEE; color: #D32F2F; }
        .nf-alert.warning { background: #FFF3E0; color: #E65100; }

        /* מסך הגדרות */
        #nf-settings-view { display: none; }
        .nf-input-group { margin-bottom: 12px; }
        .nf-input-group label { display: block; font-size: 12px; color: #757575; margin-bottom: 4px; font-weight:500;}
        .nf-input-group input, .nf-input-group select {
            width: 100%; padding: 8px; border: 1px solid #E0E0E0;
            border-radius: 8px; box-sizing: border-box; font-size: 13px;
        }
        .nf-btn-save {
            background: #2196F3; color: white; border: none; padding: 10px;
            width: 100%; border-radius: 8px; cursor: pointer; font-weight: 500;
            transition: background 0.2s; margin-top: 5px;
        }
        .nf-btn-save:hover { background: #1976D2; }

        /* הערה קופצת (Tooltip) */
        .nf-tooltip-wrap { position: relative; display: inline-block; cursor: help; margin-right: 4px; }
        .nf-tooltip-icon { background: #E0E0E0; color: #616161; border-radius: 50%; width: 14px; height: 14px; display: inline-flex; justify-content: center; align-items: center; font-size: 10px; font-weight: bold; }
        .nf-tooltip-text {
            visibility: hidden; width: 200px; background-color: #424242; color: #fff; text-align: right;
            border-radius: 6px; padding: 8px; position: absolute; z-index: 1; bottom: 125%; right: 0;
            opacity: 0; transition: opacity 0.3s; font-size: 11px; line-height: 1.4; font-weight: 400;
        }
        .nf-tooltip-wrap:hover .nf-tooltip-text { visibility: visible; opacity: 1; }

        .nf-footer {
            border-top: 1px solid #EEEEEE; padding: 10px; text-align: center; background: #FAFAFA;
        }
        .nf-footer a { color: #2196F3; text-decoration: none; font-size: 12px; font-weight: 500; }
    `);

    // --- בניית ה-HTML ב-DOM ---
    const root = document.createElement('div');
    root.id = 'nf-flutter-tracker-wrap';
    root.innerHTML = `
        <div id="nf-flutter-tracker-popup">
            <div class="nf-header">
                <span id="nf-header-title">מעקב חבילת גלישה</span>
                <div style="display:flex; gap: 8px;">
                    <button class="nf-icon-btn" id="nf-btn-refresh" title="רענן נתונים">
                        <svg viewBox="0 0 24 24"><path d="M17.65,6.35C16.2,4.9,14.21,4,12,4c-4.42,0-7.99,3.58-7.99,8s3.57,8,7.99,8c3.73,0,6.84-2.55,7.73-6h-2.08 c-0.82,2.33-3.04,4-5.65,4c-3.31,0-6-2.69-6-6s2.69-6,6-6c1.66,0,3.14,0.69,4.22,1.78L13,11h7V4L17.65,6.35z"/></svg>
                    </button>
                    <button class="nf-icon-btn" id="nf-btn-settings" title="הגדרות">
                        <svg viewBox="0 0 24 24"><path d="M19.14,12.94c0.04-0.3,0.06-0.61,0.06-0.94c0-0.32-0.02-0.64-0.06-0.94l2.03-1.58c0.18-0.14,0.23-0.41,0.12-0.61 l-1.92-3.32c-0.12-0.22-0.37-0.29-0.59-0.22l-2.39,0.96c-0.5-0.38-1.03-0.7-1.62-0.94L14.4,2.81c-0.04-0.24-0.24-0.41-0.48-0.41 h-3.84c-0.24,0-0.43,0.17-0.47,0.41L9.25,5.35C8.66,5.59,8.12,5.92,7.63,6.29L5.24,5.33c-0.22-0.08-0.47,0-0.59,0.22L2.73,8.87 C2.62,9.08,2.66,9.34,2.86,9.48l2.03,1.58C4.84,11.36,4.8,11.69,4.8,12s0.02,0.64,0.06,0.94l-2.03,1.58 c-0.18,0.14-0.23,0.41-0.12,0.61l1.92,3.32c0.12,0.22,0.37,0.29,0.59,0.22l2.39-0.96c0.5,0.38,1.03,0.7,1.62,0.94l0.36,2.54 c0.05,0.24,0.24,0.41,0.48,0.41h3.84c0.24,0,0.43-0.17,0.47-0.41l0.36-2.54c0.59-0.24,1.13-0.56,1.62-0.94l2.39,0.96 c0.22,0.08,0.47,0,0.59-0.22l1.92-3.32c0.12-0.22,0.07-0.49-0.12-0.61L19.14,12.94z M12,15.6c-1.98,0-3.6-1.62-3.6-3.6 s1.62-3.6,3.6-3.6s3.6,1.62,3.6,3.6S13.98,15.6,12,15.6z"/></svg>
                    </button>
                </div>
            </div>

            <div id="nf-main-view" class="nf-body">
                <div class="nf-data-row"><span>נוצל עד כה:</span> <strong id="nf-val-used">טוען...</strong></div>
                <div class="nf-data-row"><span>סה"כ בחבילה:</span> <strong id="nf-val-total">-</strong></div>
                <div class="nf-data-row"><span>תקין להיום:</span> <strong id="nf-val-expected">-</strong></div>

                <div class="nf-progress-wrap">
                    <div class="nf-progress-bg"><div id="nf-progress" class="nf-progress-bar" style="width: 0%;"></div></div>
                    <div id="nf-progress-marker" class="nf-progress-marker" title="הקו מייצג את האחוז בו היית אמור להיות היום" style="right: 0%;"></div>
                </div>

                <div id="nf-status-box" class="nf-alert warning">טוען נתונים...</div>
            </div>

            <div id="nf-settings-view" class="nf-body">
                <div class="nf-input-group">
                    <label>תאריך התחלת חבילה <span class="nf-tooltip-wrap"><span class="nf-tooltip-icon">i</span><span class="nf-tooltip-text">לדוגמה: אם החבילה התחילה ב-15.01, הכנס תאריך זה. המערכת תדע לחשב מחזורים עתידיים לבד.</span></span></label>
                    <input type="date" id="nf-inp-date">
                </div>
                <div class="nf-input-group">
                    <label>גודל חבילה כולל (GB)</label>
                    <input type="number" id="nf-inp-gb" min="1" step="0.5" placeholder="לדוגמה 100">
                </div>
                <div class="nf-input-group">
                    <label>חישוב שישי-שבת</label>
                    <select id="nf-inp-weekend">
                        <option value="two">חשב כשני ימים נפרדים (רגיל)</option>
                        <option value="one">חשב סופ"ש כיום אחד בלבד</option>
                    </select>
                </div>
                <button id="nf-btn-save" class="nf-btn-save">שמור</button>
            </div>

            <div class="nf-footer">
                <a href="https://netfree.link/app/#/user-details" target="_blank">פרטי החשבון המלאים בנטפרי</a>
            </div>
        </div>

        <div id="nf-flutter-tracker-fab" title="הצג נתוני גלישה">
            <svg viewBox="0 0 24 24"><path d="M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6z"/></svg>
        </div>
    `;
    document.body.appendChild(root);

    // רפרנסים ל-DOM
    const ui = {
        fab: document.getElementById('nf-flutter-tracker-fab'),
        popup: document.getElementById('nf-flutter-tracker-popup'),
        btnSettings: document.getElementById('nf-btn-settings'),
        btnRefresh: document.getElementById('nf-btn-refresh'),
        btnSave: document.getElementById('nf-btn-save'),
        viewMain: document.getElementById('nf-main-view'),
        viewSettings: document.getElementById('nf-settings-view'),

        valUsed: document.getElementById('nf-val-used'),
        valTotal: document.getElementById('nf-val-total'),
        valExpected: document.getElementById('nf-val-expected'),
        progressBar: document.getElementById('nf-progress'),
        progressMarker: document.getElementById('nf-progress-marker'),
        statusBox: document.getElementById('nf-status-box'),

        inpDate: document.getElementById('nf-inp-date'),
        inpGb: document.getElementById('nf-inp-gb'),
        inpWeekend: document.getElementById('nf-inp-weekend'),
    };

    // --- אתחול הגדרות ---
    function loadSettings() {
        try {
            const raw = GM_getValue(STORAGE_KEY, '');
            if (!raw) return { ...DEFAULT_SETTINGS };
            return { ...DEFAULT_SETTINGS, ...JSON.parse(raw) };
        } catch { return { ...DEFAULT_SETTINGS }; }
    }

    function saveSettings() {
        GM_setValue(STORAGE_KEY, JSON.stringify(state.settings));
    }

    // צימוד הגדרות למסך העריכה
    ui.inpDate.value = state.settings.packageStartDate;
    ui.inpGb.value = state.settings.packageQuotaGb;
    ui.inpWeekend.value = state.settings.weekendMode;

    // --- אירועי משתמש ---
    ui.fab.addEventListener('click', () => {
        ui.popup.classList.toggle('show');
        if (ui.popup.classList.contains('show') && !state.data) refreshData();
    });

    ui.btnSettings.addEventListener('click', () => {
        const isSettings = ui.viewSettings.style.display === 'block';
        ui.viewSettings.style.display = isSettings ? 'none' : 'block';
        ui.viewMain.style.display = isSettings ? 'block' : 'none';
    });

    ui.btnRefresh.addEventListener('click', refreshData);

    ui.btnSave.addEventListener('click', () => {
        state.settings.packageStartDate = ui.inpDate.value || '';
        state.settings.packageQuotaGb = ui.inpGb.value || '';
        state.settings.weekendMode = ui.inpWeekend.value || 'two';
        saveSettings();

        ui.viewSettings.style.display = 'none';
        ui.viewMain.style.display = 'block';
        refreshData();
    });

    // --- תקשורת מול שרתי נטפרי (Cross-Origin Setup) ---
    function request(url, options = {}) {
        return new Promise((resolve, reject) => {
            GM_xmlhttpRequest({
                method: options.method || 'GET',
                url,
                headers: options.headers || {},
                data: options.body || null,
                onload: (res) => res.status >= 200 && res.status < 300 ? resolve(res.responseText) : reject(new Error(`HTTP ${res.status}`)),
                onerror: () => reject(new Error(`Network Error`))
            });
        });
    }

    async function fetchUsageData() {
        const nfUserText = await request(`https://user-info.internal.netfree.link/user/${Math.random()}`);
        const nfUser = JSON.parse(nfUserText);
        if (!nfUser || !nfUser.enc) throw new Error('שגיאה בזיהוי משתמש נטפרי');

        const linkInfoText = await request('https://netfree.link/api/user/link-info', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ enc: nfUser.enc })
        });

        const linkInfo = JSON.parse(linkInfoText);
        return parseUsageText(linkInfo.monthlyUsages || '');
    }

    function parseUsageText(text) {
        const amountMatch = String(text).match(/([\d.]+)\s*([KMGT]?B)/i);
        let usedGb = NaN;
        if (amountMatch) {
            const value = parseFloat(amountMatch[1]);
            const unit = amountMatch[2].toUpperCase();
            const factor = { KB: 1/(1024*1024), MB: 1/1024, GB: 1, TB: 1024 }[unit] || 1;
            usedGb = value * factor;
        }
        return usedGb;
    }

    // --- לוגיקת חישוב ימים ומחזורים מדויקת ---
    function startOfDay(d) { return new Date(d.getFullYear(), d.getMonth(), d.getDate()); }
    function addDays(d, num) { const r = new Date(d); r.setDate(r.getDate() + num); return startOfDay(r); }

    function countUnits(startDate, endDate, weekendMode) {
        const start = startOfDay(startDate);
        const end = startOfDay(endDate);
        if (start > end) return 0;

        let count = 0, cursor = new Date(start);
        while (cursor <= end) {
            const day = cursor.getDay(); // 0=Sun, 5=Fri, 6=Sat
            if (weekendMode === 'one') {
                if (day === 5 && addDays(cursor, 1) <= end) { count++; cursor = addDays(cursor, 2); continue; }
                if (day === 6) { count++; cursor = addDays(cursor, 1); continue; }
            }
            count++;
            cursor = addDays(cursor, 1);
        }
        return count;
    }

    function getCycleDates() {
        if (!state.settings.packageStartDate) return null;
        const initStart = new Date(state.settings.packageStartDate);
        if (isNaN(initStart)) return null;

        const today = startOfDay(new Date());
        const desiredDay = initStart.getDate();

        // מציאת מחזור נוכחי בהתחשבות בחודשים בעלי פחות מ-31 יום
        let cycleStart = new Date(today.getFullYear(), today.getMonth(), Math.min(desiredDay, new Date(today.getFullYear(), today.getMonth()+1, 0).getDate()));
        if (cycleStart > today) {
            cycleStart = new Date(today.getFullYear(), today.getMonth()-1, Math.min(desiredDay, new Date(today.getFullYear(), today.getMonth(), 0).getDate()));
        }

        const nextCycleStart = new Date(cycleStart.getFullYear(), cycleStart.getMonth()+1, Math.min(desiredDay, new Date(cycleStart.getFullYear(), cycleStart.getMonth()+2, 0).getDate()));
        const cycleEnd = addDays(nextCycleStart, -1);

        return { cycleStart, cycleEnd, today };
    }

    // --- מנוע תחזית סיום חבילה ---
    function predictExhaustionDate(today, remainingGb, ratePerUnit, weekendMode) {
        if (ratePerUnit <= 0) return null;
        let unitsLeft = remainingGb / ratePerUnit;
        if (unitsLeft <= 0) return today;

        let cursor = new Date(today);
        let wholeUnits = Math.ceil(unitsLeft); // מעגלים למעלה (אם נשאר 2.1 ימים, זה ייגמר במהלך היום השלישי)

        while (wholeUnits > 0) {
            cursor.setDate(cursor.getDate() + 1); // מקדמים מחר
            if (weekendMode === 'one' && cursor.getDay() === 6) {
                // במצב ששישי ושבת הם יום אחד, יום שבת לא "מבזבז" יחידה לוגית, כי היא בוזבזה בשישי
            } else {
                wholeUnits--;
            }
        }
        return cursor;
    }

    // --- עדכון נתונים ותצוגה ---
    async function refreshData() {
        if (state.loading) return;
        state.loading = true;

        ui.valUsed.innerText = 'מחשב...';
        ui.statusBox.className = 'nf-alert warning';
        ui.statusBox.innerText = 'מקבל נתונים...';

        try {
            const usedGb = await fetchUsageData();
            state.data = { usedGb };
            render();
        } catch (err) {
            ui.statusBox.className = 'nf-alert danger';
            ui.statusBox.innerText = 'שגיאה בחיבור לנטפרי. ודא שאתה מחובר.';
        } finally {
            state.loading = false;
        }
    }

    function render() {
        if (!state.data) return;
        const usedGb = state.data.usedGb;
        const totalGb = parseFloat(state.settings.packageQuotaGb);
        const dates = getCycleDates();

        ui.valUsed.innerText = isNaN(usedGb) ? 'לא זוהה' : `${usedGb.toFixed(2)} GB`;

        // אם אין הגדרות מוגדרות
        if (isNaN(totalGb) || !dates) {
            ui.valTotal.innerText = 'הגדר חבילה';
            ui.valExpected.innerText = 'הגדר תאריך';
            ui.statusBox.className = 'nf-alert warning';
            ui.statusBox.innerHTML = '<strong>חסרים נתונים!</strong><br>לחץ על גלגל השיניים להגדרת תאריך ההתחלה וגודל החבילה.';
            ui.progressBar.style.width = '0%';
            ui.progressMarker.style.display = 'none';
            return;
        }

        ui.valTotal.innerText = `${totalGb.toFixed(1)} GB`;

        const totalUnits = countUnits(dates.cycleStart, dates.cycleEnd, state.settings.weekendMode);
        const elapsedUnits = countUnits(dates.cycleStart, dates.today, state.settings.weekendMode);

        const expectedGb = (totalGb / totalUnits) * elapsedUnits;
        ui.valExpected.innerText = `${expectedGb.toFixed(2)} GB`;

        const usedPercent = Math.min((usedGb / totalGb) * 100, 100);
        const expectedPercent = Math.min((elapsedUnits / totalUnits) * 100, 100);

        ui.progressBar.style.width = `${usedPercent}%`;
        ui.progressMarker.style.display = 'block';
        ui.progressMarker.style.right = `${expectedPercent}%`; // right כי זה RTL

        if (usedGb > expectedGb) {
            ui.progressBar.classList.add('danger');

            // חישוב תחזית לחריגה
            let projectionHtml = '';
            if (elapsedUnits > 0) {
                const ratePerUnit = usedGb / elapsedUnits;
                const remainingGb = totalGb - usedGb;

                if (remainingGb > 0) {
                    const exhaustDate = predictExhaustionDate(dates.today, remainingGb, ratePerUnit, state.settings.weekendMode);
                    if (exhaustDate) {
                        const daysOfWeek = ['ראשון', 'שני', 'שלישי', 'רביעי', 'חמישי', 'שישי', 'שבת'];
                        let dayName = daysOfWeek[exhaustDate.getDay()];

                        // טיפול מיוחד בתצוגה של יום שישי/שבת המאוחדים
                        if (state.settings.weekendMode === 'one' && (exhaustDate.getDay() === 5 || exhaustDate.getDay() === 6)) {
                            dayName = 'שישי/שבת';
                        }

                        const dateStr = `${exhaustDate.getDate().toString().padStart(2,'0')}/${(exhaustDate.getMonth()+1).toString().padStart(2,'0')}`;

                        projectionHtml = `
                            <div style="margin-top: 8px; padding-top: 8px; border-top: 1px solid rgba(211, 47, 47, 0.2); font-size: 11.5px; color: #B71C1C;">
                                📉 <b>תחזית:</b> בקצב הנוכחי, החבילה תתרוקן לחלוטין ביום <b>${dayName}</b> (${dateStr}).
                            </div>
                        `;
                    }
                } else {
                    projectionHtml = `<div style="margin-top: 8px; padding-top: 8px; border-top: 1px solid rgba(211, 47, 47, 0.2); font-size: 11.5px;">🚫 <b>החבילה שלך הסתיימה לחלוטין!</b></div>`;
                }
            }

            ui.statusBox.className = 'nf-alert danger';
            ui.statusBox.innerHTML = `⚠️ <b>חריגה יחסית:</b><br>ניצלת ${(usedGb - expectedGb).toFixed(2)} GB יותר מהקצב המומלץ להיום.${projectionHtml}`;
        } else {
            ui.progressBar.classList.remove('danger');
            ui.statusBox.className = 'nf-alert';
            ui.statusBox.innerHTML = `✅ <b>מצב תקין:</b><br>אתה ${(expectedGb - usedGb).toFixed(2)} GB מתחת לתקרת השימוש היומית.`;
        }
    }

    // טעינה ראשונית וטיימר ריענון אוטומטי
    refreshData();
    setInterval(refreshData, REFRESH_MS);

})();