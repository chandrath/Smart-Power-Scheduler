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

    var rawDur = settings.duration !== undefined ? settings.duration.toString().trim() : "30s";
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

function saveSettings() {
    if (!websocket || websocket.readyState !== WebSocket.OPEN) return;

    var actionSelect = document.getElementById("actionSelect");
    var presetSelect = document.getElementById("presetSelect");
    var customInput = document.getElementById("customDurationInput");
    var soundCheckbox = document.getElementById("soundCheckbox");

    var duration = "30s";
    if (presetSelect.value === "custom") {
        duration = customInput.value.trim() || "30s";
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
