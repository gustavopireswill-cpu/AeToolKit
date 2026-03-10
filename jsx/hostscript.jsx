var AeKitTools = AeKitTools || {};

AeKitTools.run = function (action) {
    try {
        switch (action) {
        case "precomposeSelected":
            return AeKitTools.precomposeSelected();
        case "deleteSelected":
            return AeKitTools.deleteSelected();
        case "createCamera3D":
            return AeKitTools.createCamera3D();
        case "organizeProject":
            return AeKitTools.organizeProject();
        case "createSolidWithPicker":
            return AeKitTools.createSolidWithPicker();
        case "clearCache":
            return AeKitTools.clearCache();
        default:
            return AeKitTools.fail("Acao desconhecida: " + action);
        }
    } catch (error) {
        return AeKitTools.fail(error.toString());
    }
};

AeKitTools.precomposeSelected = function () {
    var comp = AeKitTools.getActiveComp();
    var layers;
    var indices = [];
    var i;
    var suggestedName;

    if (!comp) {
        return AeKitTools.fail("Abra uma composicao e selecione as camadas para precompor.");
    }

    layers = comp.selectedLayers;
    if (!layers || !layers.length) {
        return AeKitTools.fail("Nenhuma camada selecionada na timeline.");
    }

    for (i = 0; i < layers.length; i += 1) {
        indices.push(layers[i].index);
    }

    indices.sort(function (left, right) {
        return left - right;
    });

    suggestedName = layers.length === 1 ? (layers[0].name + " Precomp") : "Precomp " + AeKitTools.timestamp();
    suggestedName = AeKitTools.uniqueCompName(suggestedName);

    app.beginUndoGroup("AeKitTools - Precompose");
    comp.layers.precompose(indices, suggestedName, true);
    app.endUndoGroup();

    return AeKitTools.success("Precomp criada: " + suggestedName + ".");
};

AeKitTools.deleteSelected = function () {
    var comp = AeKitTools.getActiveComp();
    var layers;
    var i;
    var removed = 0;

    if (!comp) {
        return AeKitTools.fail("Abra uma composicao antes de excluir camadas.");
    }

    layers = comp.selectedLayers;
    if (!layers || !layers.length) {
        return AeKitTools.fail("Nenhuma camada selecionada para excluir.");
    }

    app.beginUndoGroup("AeKitTools - Delete Selected");

    for (i = layers.length - 1; i >= 0; i -= 1) {
        layers[i].remove();
        removed += 1;
    }

    app.endUndoGroup();

    return AeKitTools.success(removed + " camada(s) removida(s).");
};

AeKitTools.createCamera3D = function () {
    var comp = AeKitTools.getActiveComp();
    var camera;

    if (!comp) {
        return AeKitTools.fail("Abra uma composicao para criar a camera 3D.");
    }

    app.beginUndoGroup("AeKitTools - Create Camera");
    camera = comp.layers.addCamera("Camera 3D", [comp.width / 2, comp.height / 2]);
    camera.autoOrient = AutoOrientType.NO_AUTO_ORIENT;
    camera.moveToBeginning();
    app.endUndoGroup();

    return AeKitTools.success("Camera 3D criada no centro da composicao.");
};

AeKitTools.organizeProject = function () {
    var project = app.project;
    var folderNames = [
        "Comps",
        "Precomps",
        "Solids",
        "Images",
        "Videos",
        "Audio",
        "Graphics",
        "Placeholders",
        "Missing",
        "Footage"
    ];
    var folders = {};
    var usedCompIds = {};
    var root = project.rootFolder;
    var i;
    var item;
    var destination;
    var moved = 0;
    var skipped = 0;

    if (!project || !project.numItems) {
        return AeKitTools.fail("O projeto esta vazio.");
    }

    app.beginUndoGroup("AeKitTools - Organize Project");

    for (i = 0; i < folderNames.length; i += 1) {
        folders[folderNames[i]] = AeKitTools.getOrCreateFolder(folderNames[i], root);
    }

    usedCompIds = AeKitTools.collectReferencedCompIds();

    for (i = 1; i <= project.numItems; i += 1) {
        item = project.item(i);

        if (!item || item instanceof FolderItem) {
            continue;
        }

        if (!AeKitTools.isMovableFromFolder(item.parentFolder, root, folders)) {
            skipped += 1;
            continue;
        }

        destination = AeKitTools.getDestinationFolder(item, folders, usedCompIds);

        if (!destination) {
            skipped += 1;
            continue;
        }

        if (item.parentFolder !== destination) {
            item.parentFolder = destination;
            moved += 1;
        }
    }

    app.endUndoGroup();

    return AeKitTools.success("Projeto organizado. Itens movidos: " + moved + ". Ignorados: " + skipped + ".");
};

AeKitTools.createSolidWithPicker = function () {
    var comp = AeKitTools.getActiveComp();
    var pickedColor;
    var color;
    var solidName;
    var solid;

    if (!comp) {
        return AeKitTools.fail("Abra uma composicao para criar um solido.");
    }

    pickedColor = $.colorPicker();
    if (pickedColor === -1) {
        return AeKitTools.fail("Criacao do solido cancelada.");
    }

    color = AeKitTools.hexToColorArray(pickedColor);
    solidName = "Solid " + AeKitTools.hexLabel(pickedColor);

    app.beginUndoGroup("AeKitTools - Create Solid");
    solid = comp.layers.addSolid(color, solidName, comp.width, comp.height, comp.pixelAspect, comp.duration);
    solid.moveToBeginning();
    app.endUndoGroup();

    return AeKitTools.success("Solido criado com a cor #" + AeKitTools.hexLabel(pickedColor) + ".");
};

AeKitTools.clearCache = function () {
    app.purge(PurgeTarget.ALL_CACHES);
    return AeKitTools.success("Caches do After Effects foram limpos.");
};

AeKitTools.getActiveComp = function () {
    var item = app.project ? app.project.activeItem : null;
    return (item && item instanceof CompItem) ? item : null;
};

AeKitTools.collectReferencedCompIds = function () {
    var result = {};
    var project = app.project;
    var i;
    var j;
    var item;
    var layer;

    for (i = 1; i <= project.numItems; i += 1) {
        item = project.item(i);

        if (!(item instanceof CompItem)) {
            continue;
        }

        for (j = 1; j <= item.numLayers; j += 1) {
            layer = item.layer(j);

            if (layer instanceof AVLayer && layer.source && layer.source instanceof CompItem) {
                result[layer.source.id] = true;
            }
        }
    }

    return result;
};

AeKitTools.getDestinationFolder = function (item, folders, usedCompIds) {
    if (item instanceof CompItem) {
        return usedCompIds[item.id] ? folders.Precomps : folders.Comps;
    }

    if (!(item instanceof FootageItem)) {
        return folders.Footage;
    }

    if (item.footageMissing) {
        return folders.Missing;
    }

    if (item.mainSource instanceof SolidSource) {
        return folders.Solids;
    }

    if (item.mainSource instanceof PlaceholderSource) {
        return folders.Placeholders;
    }

    return AeKitTools.folderForExtension(item, folders);
};

AeKitTools.folderForExtension = function (item, folders) {
    var extension = "";
    var match;

    if (item.file && item.file.name) {
        match = item.file.name.match(/\.([^.]+)$/);
        extension = match ? match[1].toLowerCase() : "";
    }

    if (AeKitTools.inArray(extension, ["jpg", "jpeg", "png", "gif", "bmp", "tif", "tiff", "tga", "iff", "exr", "dpx"])) {
        return folders.Images;
    }

    if (AeKitTools.inArray(extension, ["mov", "mp4", "m4v", "avi", "mxf", "webm", "mkv", "wmv"])) {
        return folders.Videos;
    }

    if (AeKitTools.inArray(extension, ["mp3", "wav", "aif", "aiff", "flac", "ogg", "m4a"])) {
        return folders.Audio;
    }

    if (AeKitTools.inArray(extension, ["psd", "ai", "eps", "pdf"])) {
        return folders.Graphics;
    }

    return folders.Footage;
};

AeKitTools.isMovableFromFolder = function (parentFolder, rootFolder, folders) {
    var key;

    if (!parentFolder || parentFolder === rootFolder) {
        return true;
    }

    for (key in folders) {
        if (folders.hasOwnProperty(key) && folders[key] === parentFolder) {
            return true;
        }
    }

    return false;
};

AeKitTools.getOrCreateFolder = function (name, parentFolder) {
    var project = app.project;
    var i;
    var item;

    for (i = 1; i <= project.numItems; i += 1) {
        item = project.item(i);
        if (item instanceof FolderItem && item.name === name && item.parentFolder === parentFolder) {
            return item;
        }
    }

    item = project.items.addFolder(name);
    item.parentFolder = parentFolder;
    return item;
};

AeKitTools.uniqueCompName = function (baseName) {
    var project = app.project;
    var name = baseName;
    var counter = 2;

    while (AeKitTools.projectHasCompNamed(name)) {
        name = baseName + " " + counter;
        counter += 1;
    }

    return name;
};

AeKitTools.projectHasCompNamed = function (name) {
    var project = app.project;
    var i;
    var item;

    for (i = 1; i <= project.numItems; i += 1) {
        item = project.item(i);
        if (item instanceof CompItem && item.name === name) {
            return true;
        }
    }

    return false;
};

AeKitTools.hexToColorArray = function (hexValue) {
    return [
        ((hexValue >> 16) & 255) / 255,
        ((hexValue >> 8) & 255) / 255,
        (hexValue & 255) / 255
    ];
};

AeKitTools.hexLabel = function (hexValue) {
    var text = hexValue.toString(16).toUpperCase();
    while (text.length < 6) {
        text = "0" + text;
    }
    return text;
};

AeKitTools.timestamp = function () {
    var now = new Date();
    return now.getFullYear() + "-" + AeKitTools.pad(now.getMonth() + 1) + "-" + AeKitTools.pad(now.getDate()) + " " + AeKitTools.pad(now.getHours()) + "." + AeKitTools.pad(now.getMinutes());
};

AeKitTools.pad = function (value) {
    return value < 10 ? ("0" + value) : value.toString();
};

AeKitTools.inArray = function (value, list) {
    var i;
    for (i = 0; i < list.length; i += 1) {
        if (list[i] === value) {
            return true;
        }
    }
    return false;
};

AeKitTools.success = function (message) {
    return AeKitTools.respond(true, message);
};

AeKitTools.fail = function (message) {
    return AeKitTools.respond(false, message);
};

AeKitTools.respond = function (ok, message) {
    return '{"ok":' + (ok ? "true" : "false") + ',"message":"' + AeKitTools.escape(message) + '"}';
};

AeKitTools.escape = function (text) {
    return String(text)
        .replace(/\\/g, "\\\\")
        .replace(/"/g, '\\"')
        .replace(/\r/g, "\\r")
        .replace(/\n/g, "\\n");
};
