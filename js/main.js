(function () {
    "use strict";

    var csInterface = new CSInterface();
    var statusNode = document.getElementById("status");
    var buttons = Array.prototype.slice.call(document.querySelectorAll(".tool-button"));
    var statusTimer = null;

    function init() {
        bindButtons();
        applyHostTheme();
        watchThemeChanges();
        showStatus("AeKitTools pronto.", "success", 1800);
    }

    function bindButtons() {
        buttons.forEach(function (button) {
            button.addEventListener("click", function () {
                runAction(button);
            });
        });
    }

    function runAction(button) {
        var action = button.getAttribute("data-action");
        var label = button.getAttribute("data-label") || "ferramenta";

        if (!action || button.classList.contains("is-busy")) {
            return;
        }

        button.classList.add("is-busy");
        showStatus("Executando " + label.toLowerCase() + "...", "", 0);

        csInterface.evalScript('AeKitTools.run("' + action + '")', function (rawResult) {
            button.classList.remove("is-busy");
            handleScriptResult(rawResult, label);
        });
    }

    function handleScriptResult(rawResult, label) {
        var payload = parsePayload(rawResult);

        if (!payload.ok) {
            showStatus(payload.message || ("Falha ao executar " + label + "."), "error", 4200);
            return;
        }

        showStatus(payload.message || (label + " executado."), "success", 2600);
    }

    function parsePayload(rawResult) {
        if (!rawResult || rawResult === "EvalScript error.") {
            return {
                ok: false,
                message: "After Effects nao respondeu ao painel."
            };
        }

        try {
            return JSON.parse(rawResult);
        } catch (error) {
            return {
                ok: false,
                message: rawResult
            };
        }
    }

    function showStatus(message, kind, duration) {
        window.clearTimeout(statusTimer);

        statusNode.textContent = message;
        statusNode.className = "status is-visible";

        if (kind === "error") {
            statusNode.classList.add("is-error");
        } else if (kind === "success") {
            statusNode.classList.add("is-success");
        }

        if (duration !== 0) {
            statusTimer = window.setTimeout(function () {
                statusNode.classList.remove("is-visible");
            }, duration || 2400);
        }
    }

    function watchThemeChanges() {
        csInterface.addEventListener(CSInterface.THEME_COLOR_CHANGED_EVENT, function () {
            applyHostTheme();
        });
    }

    function applyHostTheme() {
        var hostEnv = csInterface.getHostEnvironment();
        var skinInfo = hostEnv && hostEnv.appSkinInfo ? hostEnv.appSkinInfo : null;
        var panelColor = getColor(skinInfo && skinInfo.panelBackgroundColor, { red: 24, green: 24, blue: 26 });
        var highlightColor = getColor(skinInfo && skinInfo.systemHighlightColor, null);
        var accentColor = highlightColor || deriveAccent(panelColor);
        var accentSoft = toRgba(accentColor, 0.36);

        document.documentElement.style.setProperty("--app-bg", "transparent");
        document.documentElement.style.setProperty("--rail-bg", toRgba(adjust(panelColor, -8), 0.94));
        document.documentElement.style.setProperty("--rail-bg-top", toRgba(adjust(panelColor, 10), 0.96));
        document.documentElement.style.setProperty("--accent", toRgb(accentColor));
        document.documentElement.style.setProperty("--accent-soft", accentSoft);
    }

    function getColor(colorNode, fallback) {
        if (!colorNode || !colorNode.color) {
            return fallback;
        }

        return {
            red: numberOr(colorNode.color.red, fallback ? fallback.red : 24),
            green: numberOr(colorNode.color.green, fallback ? fallback.green : 24),
            blue: numberOr(colorNode.color.blue, fallback ? fallback.blue : 26)
        };
    }

    function deriveAccent(panelColor) {
        var brightness = (panelColor.red * 0.299) + (panelColor.green * 0.587) + (panelColor.blue * 0.114);
        return brightness < 120 ? adjust(panelColor, 92) : adjust(panelColor, -72);
    }

    function adjust(color, delta) {
        return {
            red: clamp(color.red + delta),
            green: clamp(color.green + delta),
            blue: clamp(color.blue + delta)
        };
    }

    function toRgb(color) {
        return "rgb(" + color.red + ", " + color.green + ", " + color.blue + ")";
    }

    function toRgba(color, alpha) {
        return "rgba(" + color.red + ", " + color.green + ", " + color.blue + ", " + alpha + ")";
    }

    function clamp(value) {
        return Math.max(0, Math.min(255, Math.round(value)));
    }

    function numberOr(value, fallback) {
        return typeof value === "number" ? value : fallback;
    }

    init();
}());
