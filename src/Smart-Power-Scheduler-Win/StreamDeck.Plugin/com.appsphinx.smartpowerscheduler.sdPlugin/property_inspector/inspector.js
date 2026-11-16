var websocket = null;
var uuid = null;
var actionInfo = {};
var settings = {};

function connectElgatoStreamDeckSocket(inPort, inPropertyInspectorUUID, inRegisterEvent, inInfo, inActionInfo) {
    uuid = inPropertyInspectorUUID;
    actionInfo = JSON.parse(inActionInfo);
    settings = (actionInfo.payload && actionInfo.payload.settings) ? actionInfo.payload.settings : {};

    websocket = new WebSocket("ws://127.0.0.1:" + inPort);

    websocket.onopen = function () {
        var registerJson = {
            event: inRegisterEvent,
            uuid: inPropertyInspectorUUID
        };
        websocket.send(JSON.stringify(registerJson));
        initFormValues();
    };

    websocket.onmessage = function (evt) {
        var data = JSON.parse(evt.data);
        if (data.event === "didReceiveSettings") {
            settings = data.payload.settings || {};
            initFormValues();
        }
    };
}

function initFormValues() {
    var actionSelect = document.getElementById("actionSelect");
    var presetSelect = document.getElementById("presetSelect");
    var customRow = document.getElementById("customDurationRow");
    var customInput = document.getElementById("customDurationInput");
    var soundCheckbox = document.getElementById("soundCheckbox");

    if (settings.action) {
        actionSelect.value = settings.action;
    }

    var rawDur = settings.duration !== undefined ? settings.duration.toString().trim() : "10s";
    if (/^\d+$/.test(rawDur)) {
        rawDur = rawDur + "m";
    }

    var presets = ["10s", "30s", "1m", "2m", "5m", "10m", "15m", "30m", "45m", "1h", "2h", "3h", "4h"];

    if (presets.indexOf(rawDur) !== -1) {
        presetSelect.value = rawDur;
        customRow.style.display = "none";
    } else {
        presetSelect.value = "custom";
        customRow.style.display = "flex";
        customInput.value = rawDur || "30s";
    }

    soundCheckbox.checked = settings.sound !== undefined ? (settings.sound === true) : true;
}

function sanitizeDuration(val) {
    if (!val) return "30s";
    var str = val.toString().trim().toLowerCase();
    var sec = 0;
    var hm = str.match(/(\d+)\s*h/);
    var mm = str.match(/(\d+)\s*m/);
    var sm = str.match(/(\d+)\s*s/);
    if (hm) sec += parseInt(hm[1], 10) * 3600;
    if (mm) sec += parseInt(mm[1], 10) * 60;
    if (sm) sec += parseInt(sm[1], 10);
    if (!hm && !mm && !sm) {
        var n = parseInt(str, 10);
        if (!isNaN(n)) sec = n <= 12 ? n * 3600 : (n <= 720 ? n * 60 : n);
    }
    if (isNaN(sec) || sec < 5) sec = 5;
    if (sec > 43200) sec = 43200; // 12 hours max
    if (sec % 3600 === 0 && sec >= 3600) return (sec / 3600) + "h";
    if (sec % 60 === 0 && sec >= 60) return (sec / 60) + "m";
    if (sec < 60) return sec + "s";
    var h = Math.floor(sec / 3600), rem = sec % 3600, m = Math.floor(rem / 60), s = rem % 60, res = "";
    if (h > 0) res += h + "h ";
    if (m > 0) res += m + "m ";
    if (s > 0) res += s + "s";
    return res.trim();
}

function saveSettings() {
    if (!websocket || websocket.readyState !== WebSocket.OPEN) return;

    var actionSelect = document.getElementById("actionSelect");
    var presetSelect = document.getElementById("presetSelect");
    var customInput = document.getElementById("customDurationInput");
    var soundCheckbox = document.getElementById("soundCheckbox");

    var duration = "30s";
    if (presetSelect.value === "custom") {
        duration = sanitizeDuration(customInput.value);
        customInput.value = duration;
    } else {
        duration = presetSelect.value;
    }

    var newSettings = {
        action: actionSelect.value,
        duration: duration,
        sound: soundCheckbox.checked
    };

    settings = newSettings;

    var payload = {
        event: "setSettings",
        context: uuid,
        payload: newSettings
    };

    websocket.send(JSON.stringify(payload));
}

document.addEventListener("DOMContentLoaded", function () {
    var actionSelect = document.getElementById("actionSelect");
    var presetSelect = document.getElementById("presetSelect");
    var customRow = document.getElementById("customDurationRow");
    var customInput = document.getElementById("customDurationInput");
    var soundCheckbox = document.getElementById("soundCheckbox");

    presetSelect.addEventListener("change", function () {
        if (presetSelect.value === "custom") {
            customRow.style.display = "flex";
        } else {
            customRow.style.display = "none";
        }
        saveSettings();
    });

    actionSelect.addEventListener("change", saveSettings);
    customInput.addEventListener("input", saveSettings);
    soundCheckbox.addEventListener("change", saveSettings);
});
