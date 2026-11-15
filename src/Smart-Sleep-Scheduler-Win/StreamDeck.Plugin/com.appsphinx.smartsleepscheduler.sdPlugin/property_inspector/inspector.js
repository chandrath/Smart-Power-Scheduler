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

    var dur = settings.duration !== undefined ? parseInt(settings.duration, 10) : 30;
    var presets = ["5", "15", "30", "45", "60", "120", "180"];

    if (presets.indexOf(dur.toString()) !== -1) {
        presetSelect.value = dur.toString();
        customRow.style.display = "none";
    } else {
        presetSelect.value = "custom";
        customRow.style.display = "flex";
        customInput.value = dur > 0 ? dur : 30;
    }

    soundCheckbox.checked = (settings.sound === true);
}

function saveSettings() {
    if (!websocket || websocket.readyState !== WebSocket.OPEN) return;

    var actionSelect = document.getElementById("actionSelect");
    var presetSelect = document.getElementById("presetSelect");
    var customInput = document.getElementById("customDurationInput");
    var soundCheckbox = document.getElementById("soundCheckbox");

    var duration = 30;
    if (presetSelect.value === "custom") {
        duration = parseInt(customInput.value, 10);
        if (isNaN(duration) || duration < 1) duration = 1;
        if (duration > 720) duration = 720;
    } else {
        duration = parseInt(presetSelect.value, 10);
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
