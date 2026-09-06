(function () {
    "use strict";

    var config = window.jabasoftConfig || { apiBaseUrl: "http://localhost:5300" };
    var appGroups = document.getElementById("app-groups");
    var componentList = document.getElementById("component-list");
    var previewFrame = document.getElementById("preview-frame");
    var previewTitle = document.getElementById("preview-title");
    var previewOpen = document.getElementById("preview-open");
    var refreshBtn = document.getElementById("refresh-btn");
    var cssEditor = document.getElementById("css-editor");
    var saveCssBtn = document.getElementById("save-css-btn");
    var cssStatus = document.getElementById("css-status");
    var themePanel = document.getElementById("theme-panel");
    var componentPanel = document.getElementById("component-panel");
    var componentPanelTitle = document.getElementById("component-panel-title");
    var componentPreview = document.getElementById("component-preview");
    var componentCssEditor = document.getElementById("component-css-editor");
    var componentCssStatus = document.getElementById("component-css-status");
    var saveComponentCssBtn = document.getElementById("save-component-css-btn");
    var aiInstructions = document.getElementById("ai-instructions");
    var generateCssBtn = document.getElementById("generate-css-btn");
    var materializeBtn = document.getElementById("materialize-btn");
    var materializeResult = document.getElementById("materialize-result");
    var tabPages = document.getElementById("tab-pages");
    var tabComponents = document.getElementById("tab-components");
    var selectElementBtn = document.getElementById("select-element-btn");
    var showRegionsBtn = document.getElementById("show-regions-btn");
    var pickerPanel = document.getElementById("picker-panel");
    var pickerHtmlPreview = document.getElementById("picker-html-preview");
    var pickerParentBtn = document.getElementById("picker-parent-btn");
    var pickerNameInput = document.getElementById("picker-name-input");
    var pickerSaveBtn = document.getElementById("picker-save-btn");
    var pickerCancelBtn = document.getElementById("picker-cancel-btn");
    var aiSettingsBtn = document.getElementById("ai-settings-btn");
    var aiSettingsPanel = document.getElementById("ai-settings-panel");
    var aiProviderSelect = document.getElementById("ai-provider");
    var aiServerUrlInput = document.getElementById("ai-server-url");
    var aiModelSelect = document.getElementById("ai-model");
    var aiRefreshModelsBtn = document.getElementById("ai-refresh-models-btn");
    var aiTestBtn = document.getElementById("ai-test-btn");
    var aiSettingsStatus = document.getElementById("ai-settings-status");
    var aiSettingsSaveBtn = document.getElementById("ai-settings-save-btn");
    var aiSettingsCloseBtn = document.getElementById("ai-settings-close-btn");

    var lastApps = null;
    var currentApp = null;
    var currentPage = null;
    var currentComponentName = null;
    var pickerActive = false;
    var pickerSelectedEl = null;
    var regionsActive = false;
    var savedModel = "";

    // ============================================================
    // Pagina's (unchanged behavior, see loadPage/renderApps/refreshPages)
    // ============================================================

    function loadPage(app, page, buttonEl) {
        currentApp = app;
        currentPage = page;
        previewFrame.src = page.file + "?t=" + Date.now();
        previewTitle.textContent = (app.displayName || "") + " — " + (page.label || page.path) + " (kopie)";
        previewOpen.href = (app.mainUrl || app.developmentUrl) + page.path;

        var buttons = appGroups.querySelectorAll("button.page-link");
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].classList.remove("active");
        }
        if (buttonEl) {
            buttonEl.classList.add("active");
        }
    }

    function renderApps(apps) {
        lastApps = apps;
        appGroups.innerHTML = "";
        var firstApp = null;
        var firstPage = null;
        var firstButton = null;
        var appKeys = Object.keys(apps || {});

        if (appKeys.length === 0) {
            var empty = document.createElement("div");
            empty.className = "styleguide-nav-empty";
            empty.textContent = "Geen apps gevonden in appsettings.json.";
            appGroups.appendChild(empty);
            return;
        }

        appKeys.forEach(function (key) {
            var app = apps[key];
            var group = document.createElement("div");
            group.className = "app-group";

            var title = document.createElement("div");
            title.className = "app-group-title";
            title.textContent = app.displayName || key;
            group.appendChild(title);

            var pages = app.pages || [];
            if (pages.length === 0) {
                var noPages = document.createElement("div");
                noPages.className = "styleguide-nav-empty";
                noPages.textContent = "Geen pagina's geconfigureerd.";
                group.appendChild(noPages);
            }

            pages.forEach(function (page) {
                var button = document.createElement("button");
                button.type = "button";
                button.className = "page-link";
                button.textContent = page.label || page.path;
                button.addEventListener("click", function () {
                    loadPage(app, page, button);
                });
                group.appendChild(button);

                if (!firstApp) {
                    firstApp = app;
                    firstPage = page;
                    firstButton = button;
                }
            });

            appGroups.appendChild(group);
        });

        if (firstApp && firstPage) {
            loadPage(firstApp, firstPage, firstButton);
        }
    }

    function fetchAppsConfig() {
        return fetch(config.apiBaseUrl + "/api/apps-config")
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                return response.json();
            });
    }

    function refreshPages() {
        refreshBtn.disabled = true;
        previewTitle.textContent = "Pagina's kopiëren...";

        fetchAppsConfig()
            .then(function (apps) {
                renderApps(apps);
                return fetch(config.apiBaseUrl + "/api/capture-pages", { method: "POST" });
            })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                return response.json();
            })
            .then(function (results) {
                var failed = (results || []).filter(function (r) { return !r.ok; });
                if (failed.length > 0) {
                    previewTitle.textContent = failed.length + " pagina('s) konden niet gekopieerd worden (staat de app aan?).";
                } else if (lastApps) {
                    renderApps(lastApps);
                }
            })
            .catch(function (err) {
                previewTitle.textContent = "Verversen mislukt: " + err;
            })
            .finally(function () {
                refreshBtn.disabled = false;
            });
    }

    function loadCss() {
        cssStatus.textContent = "Laden...";
        fetch(config.apiBaseUrl + "/api/theme-css")
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                return response.text();
            })
            .then(function (text) {
                cssEditor.value = text;
                cssStatus.textContent = "";
            })
            .catch(function (err) {
                cssStatus.textContent = "Kon jabasoft-theme.css niet laden: " + err;
            });
    }

    function saveCss() {
        cssStatus.textContent = "Opslaan...";
        saveCssBtn.disabled = true;
        fetch(config.apiBaseUrl + "/api/theme-css", {
            method: "PUT",
            headers: { "Content-Type": "text/plain" },
            body: cssEditor.value,
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                cssStatus.textContent = "Opgeslagen. Voorbeeld wordt herladen...";
                previewFrame.src = previewFrame.src;
                setTimeout(function () { cssStatus.textContent = ""; }, 2000);
            })
            .catch(function (err) {
                cssStatus.textContent = "Opslaan mislukt: " + err;
            })
            .finally(function () {
                saveCssBtn.disabled = false;
            });
    }

    // ============================================================
    // Tabs: Pagina's / Componenten
    // ============================================================

    function showPagesTab() {
        tabPages.classList.add("active");
        tabComponents.classList.remove("active");
        appGroups.hidden = false;
        componentList.hidden = true;
        themePanel.hidden = false;
        componentPanel.hidden = true;
        selectElementBtn.disabled = false;
        showRegionsBtn.disabled = false;
    }

    function showComponentsTab() {
        tabPages.classList.remove("active");
        tabComponents.classList.add("active");
        appGroups.hidden = true;
        componentList.hidden = false;
        themePanel.hidden = true;
        // select-element/show-regions only make sense on a captured page,
        // not on a component's own isolated preview.
        selectElementBtn.disabled = true;
        showRegionsBtn.disabled = true;
        fetchComponents();
    }

    tabPages.addEventListener("click", showPagesTab);
    tabComponents.addEventListener("click", showComponentsTab);

    // ============================================================
    // Componenten: lijst + eigen preview/CSS-editor/AI-generatie/materialize
    // ============================================================

    function fetchComponents() {
        componentList.innerHTML = "Laden...";
        fetch(config.apiBaseUrl + "/api/components")
            .then(function (r) { return r.json(); })
            .then(renderComponents)
            .catch(function (err) {
                componentList.textContent = "Kon componenten niet laden: " + err;
            });
    }

    function renderComponents(components) {
        componentList.innerHTML = "";
        if (!components || components.length === 0) {
            var empty = document.createElement("div");
            empty.className = "styleguide-nav-empty";
            empty.textContent = "Nog geen componenten. Gebruik \"Selecteer element\" op een pagina.";
            componentList.appendChild(empty);
            return;
        }

        components.forEach(function (comp) {
            var button = document.createElement("button");
            button.type = "button";
            button.className = "page-link";
            button.textContent = comp.name;
            button.addEventListener("click", function () {
                var buttons = componentList.querySelectorAll("button.page-link");
                for (var i = 0; i < buttons.length; i++) {
                    buttons[i].classList.remove("active");
                }
                button.classList.add("active");
                loadComponent(comp.name);
            });
            componentList.appendChild(button);
        });

        loadComponent(components[0].name);
        componentList.querySelector("button.page-link").classList.add("active");
    }

    function loadComponent(name) {
        currentComponentName = name;
        componentPanelTitle.textContent = name;
        componentPanel.hidden = false;
        materializeResult.textContent = "";
        componentCssStatus.textContent = "Laden...";

        Promise.all([
            fetch(config.apiBaseUrl + "/api/components/" + name + "/html?t=" + Date.now()).then(function (r) { return r.text(); }),
            fetch(config.apiBaseUrl + "/api/components/" + name + "/css?t=" + Date.now()).then(function (r) { return r.ok ? r.text() : ""; }),
        ]).then(function (results) {
            var html = results[0];
            var css = results[1];
            componentCssEditor.value = css;
            componentCssStatus.textContent = "";
            renderComponentPreview(html, css);
        }).catch(function (err) {
            componentCssStatus.textContent = "Kon component niet laden: " + err;
        });
    }

    function renderComponentPreview(html, css) {
        var doc =
            "<!DOCTYPE html><html><head>" +
            "<link rel=\"stylesheet\" href=\"https://shared.jabasoft.local/jabasoft-theme.css\" />" +
            "<link rel=\"stylesheet\" href=\"https://shared.jabasoft.local/vs-theme.css\" />" +
            "<script src=\"https://shared.jabasoft.local/theme.js\"><\/script>" +
            "<style>" + css + "</style>" +
            "</head><body>" + html + "</body></html>";
        componentPreview.srcdoc = doc;
    }

    function saveComponentCss() {
        if (!currentComponentName) {
            return;
        }

        componentCssStatus.textContent = "Opslaan...";
        saveComponentCssBtn.disabled = true;
        fetch(config.apiBaseUrl + "/api/components/" + currentComponentName + "/css", {
            method: "PUT",
            headers: { "Content-Type": "text/plain" },
            body: componentCssEditor.value,
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                componentCssStatus.textContent = "Opgeslagen.";
                fetch(config.apiBaseUrl + "/api/components/" + currentComponentName + "/html?t=" + Date.now())
                    .then(function (r) { return r.text(); })
                    .then(function (html) { renderComponentPreview(html, componentCssEditor.value); });
                setTimeout(function () { componentCssStatus.textContent = ""; }, 2000);
            })
            .catch(function (err) {
                componentCssStatus.textContent = "Opslaan mislukt: " + err;
            })
            .finally(function () {
                saveComponentCssBtn.disabled = false;
            });
    }

    function generateComponentCss() {
        if (!currentComponentName) {
            return;
        }

        generateCssBtn.disabled = true;
        componentCssStatus.textContent = "AI denkt na...";
        fetch(config.apiBaseUrl + "/api/components/" + currentComponentName + "/generate-css", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ instructions: aiInstructions.value }),
        })
            .then(function (r) { return r.json(); })
            .then(function (result) {
                if (result.success) {
                    componentCssEditor.value = result.css;
                    componentCssStatus.textContent = "Voorstel geladen - controleer en klik Opslaan.";
                    fetch(config.apiBaseUrl + "/api/components/" + currentComponentName + "/html?t=" + Date.now())
                        .then(function (r) { return r.text(); })
                        .then(function (html) { renderComponentPreview(html, result.css); });
                } else {
                    componentCssStatus.textContent = "AI-generatie mislukt: " + result.errorMessage;
                }
            })
            .catch(function (err) {
                componentCssStatus.textContent = "AI-generatie mislukt: " + err;
            })
            .finally(function () {
                generateCssBtn.disabled = false;
            });
    }

    function materializeComponent() {
        if (!currentComponentName) {
            return;
        }

        materializeBtn.disabled = true;
        materializeResult.textContent = "Bezig...";
        fetch(config.apiBaseUrl + "/api/components/" + currentComponentName + "/materialize", { method: "POST" })
            .then(function (r) { return r.json(); })
            .then(function (result) {
                materializeResult.textContent =
                    "Aangemaakt: " + result.razorPath + " — plaats " + result.usageSnippet + " op de pagina waar je 'm wilt.";
            })
            .catch(function (err) {
                materializeResult.textContent = "Mislukt: " + err;
            })
            .finally(function () {
                materializeBtn.disabled = false;
            });
    }

    saveComponentCssBtn.addEventListener("click", saveComponentCss);
    generateCssBtn.addEventListener("click", generateComponentCss);
    materializeBtn.addEventListener("click", materializeComponent);

    // ============================================================
    // "Selecteer element": click an element in the preview, name it,
    // save it as a component. The preview is same-origin (everything
    // under https://app.jabasoft.local), so this reaches straight into
    // previewFrame.contentDocument - no postMessage bridging needed.
    // ============================================================

    var PICKER_STYLE_ID = "jbs-picker-style";
    var PICKER_STYLE_CSS = ".jbs-picker-hover { outline: 2px solid #ff9700 !important; outline-offset: -2px; cursor: crosshair !important; }";

    function onPickerHover(e) {
        var doc = previewFrame.contentDocument;
        var prev = doc.querySelector(".jbs-picker-hover");
        if (prev && prev !== e.target) {
            prev.classList.remove("jbs-picker-hover");
        }
        e.target.classList.add("jbs-picker-hover");
    }

    // The hover outline (see onPickerHover) is a transient class added to
    // whatever's under the cursor - without stripping it first, a captured
    // component's saved HTML would permanently include "jbs-picker-hover"
    // on whichever element happened to be hovered right before the click.
    function stripPickerArtifacts(el) {
        el.classList.remove("jbs-picker-hover");
        var inner = el.querySelectorAll(".jbs-picker-hover");
        for (var i = 0; i < inner.length; i++) {
            inner[i].classList.remove("jbs-picker-hover");
        }
    }

    function onPickerClick(e) {
        e.preventDefault();
        e.stopPropagation();
        pickerSelectedEl = e.target;
        stripPickerArtifacts(pickerSelectedEl);
        showPickerPanel();
    }

    function attachPicker() {
        var doc = previewFrame.contentDocument;
        if (!doc) {
            return;
        }

        if (!doc.getElementById(PICKER_STYLE_ID)) {
            var style = doc.createElement("style");
            style.id = PICKER_STYLE_ID;
            style.textContent = PICKER_STYLE_CSS;
            doc.head.appendChild(style);
        }

        doc.addEventListener("mouseover", onPickerHover, true);
        doc.addEventListener("click", onPickerClick, true);
    }

    function detachPicker() {
        var doc = previewFrame.contentDocument;
        if (!doc) {
            return;
        }

        doc.removeEventListener("mouseover", onPickerHover, true);
        doc.removeEventListener("click", onPickerClick, true);
        var hovered = doc.querySelector(".jbs-picker-hover");
        if (hovered) {
            hovered.classList.remove("jbs-picker-hover");
        }
    }

    function togglePicker() {
        pickerActive = !pickerActive;
        selectElementBtn.classList.toggle("active", pickerActive);
        if (pickerActive) {
            attachPicker();
        } else {
            detachPicker();
            hidePickerPanel();
        }
    }

    function showPickerPanel() {
        pickerHtmlPreview.textContent = truncate(pickerSelectedEl.outerHTML, 600);
        pickerNameInput.value = "";
        pickerPanel.hidden = false;
    }

    function hidePickerPanel() {
        pickerPanel.hidden = true;
        pickerSelectedEl = null;
    }

    function truncate(text, max) {
        return text.length > max ? text.substring(0, max) + "…" : text;
    }

    pickerParentBtn.addEventListener("click", function () {
        if (pickerSelectedEl && pickerSelectedEl.parentElement) {
            pickerSelectedEl = pickerSelectedEl.parentElement;
            stripPickerArtifacts(pickerSelectedEl);
            pickerHtmlPreview.textContent = truncate(pickerSelectedEl.outerHTML, 600);
        }
    });

    pickerCancelBtn.addEventListener("click", hidePickerPanel);

    pickerSaveBtn.addEventListener("click", function () {
        var name = pickerNameInput.value.trim();
        if (!name || !pickerSelectedEl) {
            return;
        }

        stripPickerArtifacts(pickerSelectedEl);
        pickerSaveBtn.disabled = true;
        fetch(config.apiBaseUrl + "/api/components", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                name: name,
                html: pickerSelectedEl.outerHTML,
                sourceApp: currentApp ? (currentApp.displayName || "") : "",
                sourcePath: currentPage ? currentPage.path : "",
            }),
        })
            .then(function (r) { return r.json(); })
            .then(function () {
                hidePickerPanel();
                togglePicker();
                showComponentsTab();
            })
            .catch(function (err) {
                pickerHtmlPreview.textContent = "Opslaan mislukt: " + err;
            })
            .finally(function () {
                pickerSaveBtn.disabled = false;
            });
    });

    selectElementBtn.addEventListener("click", togglePicker);

    // ============================================================
    // "Toon regio's": outline header/menu/content/footer/actionrail
    // directly in the preview, using the shared shell CSS classes every
    // captured page already has.
    // ============================================================

    var REGION_STYLE_ID = "jbs-region-style";
    var REGION_STYLE_CSS = [
        ".shell-header, .shell-menu, .shell-content, .shell-footer, .shell-actionrail { position: relative !important; }",
        ".shell-header::before, .shell-menu::before, .shell-content::before, .shell-footer::before, .shell-actionrail::before {",
        "  position: absolute; top: 0; left: 0; z-index: 99999; padding: 2px 6px;",
        "  font: bold 10px monospace; color: #fff; pointer-events: none;",
        "}",
        ".shell-header { outline: 2px dashed #e74c3c !important; }",
        ".shell-header::before { content: 'HEADER'; background: #e74c3c; }",
        ".shell-menu { outline: 2px dashed #3498db !important; }",
        ".shell-menu::before { content: 'MENU'; background: #3498db; }",
        ".shell-content { outline: 2px dashed #2ecc71 !important; }",
        ".shell-content::before { content: 'CONTENT'; background: #2ecc71; }",
        ".shell-footer { outline: 2px dashed #f39c12 !important; }",
        ".shell-footer::before { content: 'FOOTER'; background: #f39c12; }",
        ".shell-actionrail { outline: 2px dashed #9b59b6 !important; }",
        ".shell-actionrail::before { content: 'ACTIERAIL'; background: #9b59b6; }",
    ].join("\n");

    function applyRegions() {
        var doc = previewFrame.contentDocument;
        if (!doc) {
            return;
        }

        var existing = doc.getElementById(REGION_STYLE_ID);
        if (!existing) {
            var style = doc.createElement("style");
            style.id = REGION_STYLE_ID;
            style.textContent = REGION_STYLE_CSS;
            doc.head.appendChild(style);
        }
    }

    function removeRegions() {
        var doc = previewFrame.contentDocument;
        if (!doc) {
            return;
        }

        var existing = doc.getElementById(REGION_STYLE_ID);
        if (existing) {
            existing.remove();
        }
    }

    function toggleRegions() {
        regionsActive = !regionsActive;
        showRegionsBtn.classList.toggle("active", regionsActive);
        if (regionsActive) {
            applyRegions();
        } else {
            removeRegions();
        }
    }

    showRegionsBtn.addEventListener("click", toggleRegions);

    // Both picker and region-overlay inject into the preview document,
    // which is destroyed on every navigation (selecting a different page,
    // "Pagina's verversen", or the theme-CSS-save reload) - re-apply
    // whichever mode is still active once the new document is ready.
    previewFrame.addEventListener("load", function () {
        if (regionsActive) {
            applyRegions();
        }
        if (pickerActive) {
            attachPicker();
        }
    });

    // ============================================================
    // AI-instellingen (Provider/ServerUrl/Model - LM Studio by default)
    // ============================================================

    function loadAiSettings() {
        return fetch(config.apiBaseUrl + "/api/ai-connector")
            .then(function (r) { return r.json(); })
            .then(function (settings) {
                aiProviderSelect.value = settings.provider || "LmStudio";
                aiServerUrlInput.value = settings.serverUrl || "";
                savedModel = settings.model || "";
                aiModelSelect.innerHTML = "";
                if (savedModel) {
                    var opt = document.createElement("option");
                    opt.value = savedModel;
                    opt.textContent = savedModel;
                    aiModelSelect.appendChild(opt);
                }
            });
    }

    function refreshAiModels() {
        aiSettingsStatus.textContent = "Modellen ophalen...";
        fetch(config.apiBaseUrl + "/api/ai-models?provider=" + aiProviderSelect.value + "&serverUrl=" + encodeURIComponent(aiServerUrlInput.value))
            .then(function (r) { return r.json(); })
            .then(function (result) {
                aiModelSelect.innerHTML = "";
                (result.models || []).forEach(function (m) {
                    var opt = document.createElement("option");
                    opt.value = m;
                    opt.textContent = m;
                    aiModelSelect.appendChild(opt);
                });
                if (savedModel && result.models && result.models.indexOf(savedModel) !== -1) {
                    aiModelSelect.value = savedModel;
                }
                aiSettingsStatus.textContent = result.success ? (result.models.length + " model(len) gevonden.") : result.errorMessage;
            })
            .catch(function (err) {
                aiSettingsStatus.textContent = "Ophalen mislukt: " + err;
            });
    }

    function testAiConnection() {
        aiSettingsStatus.textContent = "Testen...";
        fetch(config.apiBaseUrl + "/api/ai-test-connection", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ provider: aiProviderSelect.value, serverUrl: aiServerUrlInput.value, model: aiModelSelect.value }),
        })
            .then(function (r) { return r.json(); })
            .then(function (result) {
                aiSettingsStatus.textContent = result.success ? ("Werkt: " + result.message) : ("Fout: " + result.message);
            })
            .catch(function (err) {
                aiSettingsStatus.textContent = "Test mislukt: " + err;
            });
    }

    function saveAiSettings() {
        savedModel = aiModelSelect.value || savedModel;
        fetch(config.apiBaseUrl + "/api/ai-connector", {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ provider: aiProviderSelect.value, serverUrl: aiServerUrlInput.value, model: savedModel }),
        })
            .then(function (r) {
                aiSettingsStatus.textContent = r.ok ? "Opgeslagen." : "Opslaan mislukt.";
            })
            .catch(function (err) {
                aiSettingsStatus.textContent = "Opslaan mislukt: " + err;
            });
    }

    aiSettingsBtn.addEventListener("click", function () {
        aiSettingsPanel.hidden = !aiSettingsPanel.hidden;
        if (!aiSettingsPanel.hidden) {
            loadAiSettings();
        }
    });
    aiSettingsCloseBtn.addEventListener("click", function () { aiSettingsPanel.hidden = true; });
    aiRefreshModelsBtn.addEventListener("click", refreshAiModels);
    aiTestBtn.addEventListener("click", testAiConnection);
    aiSettingsSaveBtn.addEventListener("click", saveAiSettings);

    refreshBtn.addEventListener("click", refreshPages);
    saveCssBtn.addEventListener("click", saveCss);

    // First open: capture copies right away (covers the case where none
    // exist yet) and load the CSS editor.
    refreshPages();
    loadCss();
})();
