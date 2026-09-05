(function () {
    "use strict";

    var config = window.jabasoftConfig || { apiBaseUrl: "http://localhost:5300" };
    var appGroups = document.getElementById("app-groups");
    var previewFrame = document.getElementById("preview-frame");
    var previewTitle = document.getElementById("preview-title");
    var previewOpen = document.getElementById("preview-open");
    var refreshBtn = document.getElementById("refresh-btn");
    var cssEditor = document.getElementById("css-editor");
    var saveCssBtn = document.getElementById("save-css-btn");
    var cssStatus = document.getElementById("css-status");
    var lastApps = null;

    function loadPage(app, page, buttonEl) {
        // The dummy copy (captured by /api/capture-pages) lives on this
        // same page's own origin (app.jabasoft.local), so it applies
        // theme.js/vs-theme.css itself, just like every other Jabasoft
        // page - no live cross-origin embed, no proxying at view time.
        //
        // Cache-busted on purpose: assigning the exact same src an iframe
        // already has is a no-op in the browser, so re-selecting a page
        // (or reloading after a fresh capture replaced the file on disk)
        // would otherwise keep showing whatever was there before.
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

    /// Re-reads appsettings.json (via /api/apps-config) AND re-captures a
    /// fresh local copy of every configured page (via /api/capture-pages)
    /// - the two things "Pagina's verversen" is meant to do: pick up newly
    /// added pages/apps, and refresh the dummy copies so they match what
    /// the real apps currently render.
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
                    // Re-render so the just-refreshed active page reloads too.
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

    refreshBtn.addEventListener("click", refreshPages);
    saveCssBtn.addEventListener("click", saveCss);

    // First open: capture copies right away (covers the case where none
    // exist yet) and load the CSS editor.
    refreshPages();
    loadCss();
})();
