(function (global) {
    "use strict";

    function CSInterface() {}

    CSInterface.THEME_COLOR_CHANGED_EVENT = "com.adobe.csxs.events.ThemeColorChanged";

    CSInterface.prototype.evalScript = function (script, callback) {
        if (global.__adobe_cep__ && typeof global.__adobe_cep__.evalScript === "function") {
            global.__adobe_cep__.evalScript(script, callback || function () {});
            return;
        }

        if (typeof callback === "function") {
            callback('{"ok":false,"message":"CEP host indisponivel no contexto atual."}');
        }
    };

    CSInterface.prototype.getHostEnvironment = function () {
        if (!global.__adobe_cep__ || typeof global.__adobe_cep__.getHostEnvironment !== "function") {
            return {};
        }

        try {
            return JSON.parse(global.__adobe_cep__.getHostEnvironment());
        } catch (error) {
            return {};
        }
    };

    CSInterface.prototype.addEventListener = function (type, listener) {
        if (global.__adobe_cep__ && typeof global.__adobe_cep__.addEventListener === "function") {
            global.__adobe_cep__.addEventListener(type, listener);
        }
    };

    global.CSInterface = CSInterface;
}(this));
