(function () {
    "use strict";

    var config = window.jabasoftConfig || { apps: {} };
    var nav = document.getElementById("nav");
    var content = document.getElementById("content");
    var shell = document.querySelector(".shell");
    var items = [];
    var appIndexByKey = {};
    var activeIndex = -1;

    // First in the menu, always - what a "jabasoft:home" message (posted
    // by the embedded app's own Home nav item, see the message listener
    // below) returns to.
    items.push({ label: "Home", url: "home.html" });

    Object.keys(config.apps || {}).forEach(function (key) {
        var app = config.apps[key];
        var url = app.mainUrl || app.developmentUrl;
        if (url) {
            appIndexByKey[key] = items.length;
            // "focus": embedded apps get the ENTIRE window while active
            // (see .shell.app-focus in shell.css) - no Jabasoft header,
            // menu, or action rail at all. The app itself adds a "Home"
            // item to its own menu (shown only when embedded - see
            // Shared.UI/wwwroot/embed.js) that posts a message back here.
            items.push({ label: app.displayName || key, url: url, focus: true, key: key });
        }
    });

    items.push({ label: "Instellingen", url: "settings.html" });

    // Token verbruik isn't HTML inside this WebView2 content iframe
    // anymore - it's BlazorWebView-hosted (a native sibling control, see
    // MainWindow.xaml/.cs), so selecting it posts a message to the WPF
    // host instead of setting content.src.
    items.push({ label: "Token verbruik", special: "token-dashboard" });

    function postToHost(message) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(message);
        }
    }

    // ------------------------------------------------------------
    // Shell header: "Huidige locatie"/"Actieve app" cards + rolling
    // activity log (see Shared.UI/shell-header.css for the markup/
    // styling these feed - same shape as LocalAiStudio's ShellHeader,
    // just driven from plain JS here instead of Blazor state).
    // ------------------------------------------------------------
    var headerLocation = document.getElementById("header-location");
    var headerActiveApp = document.getElementById("header-active-app");
    var headerActivityLog = document.getElementById("header-activity-log");
    var activityLines = [];

    function logActivity(text) {
        activityLines.push(text);
        if (activityLines.length > 50) {
            activityLines.shift();
        }

        headerActivityLog.innerHTML = "";
        activityLines.forEach(function (line) {
            var div = document.createElement("div");
            div.className = "log-line";
            div.textContent = line;
            headerActivityLog.appendChild(div);
        });
        headerActivityLog.scrollTop = headerActivityLog.scrollHeight;
    }

    function updateLocationCards(item) {
        headerLocation.innerHTML = "";
        var dashboard = document.createElement("span");
        dashboard.textContent = "Dashboard";
        headerLocation.appendChild(dashboard);

        if (item.focus || item.special) {
            var sep = document.createElement("span");
            sep.className = "sep";
            sep.textContent = "›";
            headerLocation.appendChild(sep);

            var current = document.createElement("span");
            current.className = "current";
            current.textContent = item.label;
            headerLocation.appendChild(current);
        }

        headerActiveApp.textContent = item.focus ? item.label : "Geen";
    }

    function activate(index) {
        activeIndex = index;

        var buttons = nav.querySelectorAll(".nav-btn");
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].classList.toggle("active", Number(buttons[i].dataset.index) === index);
        }

        var item = items[index];
        updateLocationCards(item);
        logActivity(item.label === "Home" ? "Terug naar Home" : "Geopend: " + item.label);

        if (item.special === "token-dashboard") {
            postToHost("show-token-dashboard");
            return;
        }

        postToHost("hide-token-dashboard");
        shell.classList.toggle("app-focus", !!item.focus);
        content.src = item.url;
    }

    items.forEach(function (item, index) {
        if (item.focus) {
            // Embeddable app: a row with a main button (embed here) and a
            // small "open in own window" icon - same two-icons-per-row
            // pattern as Stylebook's component list. Both just ask the
            // host what to do (see the message listener below) rather than
            // acting immediately, since the host is the one that knows
            // whether this app already has its own window open (an app is
            // never shown both embedded and in its own window at once).
            var row = document.createElement("div");
            row.className = "nav-app-row";

            var main = document.createElement("button");
            main.type = "button";
            main.className = "nav-btn";
            main.dataset.index = index;
            main.textContent = item.label;
            main.addEventListener("click", function () {
                postToHost("activate-app:" + item.key);
            });
            row.appendChild(main);

            var popout = document.createElement("button");
            popout.type = "button";
            popout.className = "nav-popout-btn";
            popout.title = "Openen in eigen venster";
            popout.textContent = "↗";
            popout.addEventListener("click", function (e) {
                e.stopPropagation();
                logActivity("Eigen venster: " + item.label);
                postToHost("open-app-window:" + item.key);
            });
            row.appendChild(popout);

            nav.appendChild(row);
        } else {
            var button = document.createElement("button");
            button.type = "button";
            button.className = "nav-btn";
            button.dataset.index = index;
            button.textContent = item.label;
            button.addEventListener("click", function () {
                activate(index);
            });
            nav.appendChild(button);
        }
    });

    if (items.length > 0) {
        activate(0);
    }

    // The embedded app's own "Home" nav item (see Shared.UI/wwwroot/
    // embed.js) posts this instead of navigating anywhere itself, since
    // Jabasoft's own menu is completely hidden while an app has focus -
    // there's nothing on this side for the user to click. This is an
    // ordinary cross-frame window.postMessage, not the WebView2 host
    // bridge below - different channel, same "message" event name.
    window.addEventListener("message", function (event) {
        if (event.data === "jabasoft:home") {
            activate(0);
        }
    });

    // Replies from the WPF host (MainWindow.xaml.cs) to "activate-app:"/
    // "open-app-window:" above - this is WebView2's host<->page bridge,
    // NOT the same channel as window.postMessage used above.
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.addEventListener("message", function (event) {
            var data = event.data;
            if (typeof data !== "string") {
                return;
            }

            if (data.indexOf("do-activate:") === 0) {
                // Host confirmed this app has no window of its own open -
                // go ahead and embed it.
                var key = data.slice("do-activate:".length);
                if (appIndexByKey.hasOwnProperty(key)) {
                    activate(appIndexByKey[key]);
                }
                return;
            }

            if (data.indexOf("app-opened-in-window:") === 0) {
                // Host just opened (or focused) this app in its own window -
                // if it was the embedded view, drop back to Home so it's
                // never shown both ways at once.
                var openedKey = data.slice("app-opened-in-window:".length);
                if (activeIndex === appIndexByKey[openedKey]) {
                    activate(0);
                }
            }
        });
    }

    // ------------------------------------------------------------
    // Shell footer meters + header "Tokens" card - polls Jabasoft's own
    // API (MainWindow.xaml.cs's /api/system-stats + /api/token-summary),
    // not the embedded apps' - see Shared.UI/shell-footer.css for the
    // markup/styling these feed.
    // ------------------------------------------------------------
    var apiBaseUrl = config.apiBaseUrl || "http://localhost:5300";
    var footerCpu = document.getElementById("footer-cpu");
    var footerCpuBar = document.getElementById("footer-cpu-bar");
    var footerRam = document.getElementById("footer-ram");
    var footerRamBar = document.getElementById("footer-ram-bar");
    var footerVram = document.getElementById("footer-vram");
    var footerVramBar = document.getElementById("footer-vram-bar");
    var footerRequests = document.getElementById("footer-requests");
    var headerTokensTotal = document.getElementById("header-tokens-total");
    var headerTokensRequests = document.getElementById("header-tokens-requests");

    function toGiB(bytes) {
        return bytes / 1024 / 1024 / 1024;
    }

    function formatTokens(value) {
        // "." as the thousands separator regardless of the OS locale - the
        // same reasoning as LocalAiStudio's ShellHeader.FormatTokens: a
        // Dutch "46.683" read the other way (as a decimal point) looks like
        // a frozen/broken counter.
        return value.toLocaleString("en-US").replace(/,/g, ".");
    }

    function pollSystemStats() {
        fetch(apiBaseUrl + "/api/system-stats")
            .then(function (r) { return r.json(); })
            .then(function (stats) {
                var cpuPercent = Math.round(stats.cpuPercent) + "%";
                footerCpu.textContent = "CPU  " + cpuPercent;
                footerCpuBar.style.setProperty("--progress", cpuPercent);

                var ramUsed = toGiB(stats.ramUsedBytes).toFixed(1);
                var ramTotal = toGiB(stats.ramTotalBytes).toFixed(1);
                footerRam.textContent = "RAM  " + ramUsed + " / " + ramTotal + " GB";
                footerRamBar.style.setProperty("--progress", (stats.ramTotalBytes ? (stats.ramUsedBytes / stats.ramTotalBytes * 100) : 0) + "%");

                if (stats.vramUsedBytes != null && stats.vramTotalBytes != null) {
                    var vramUsed = toGiB(stats.vramUsedBytes).toFixed(1);
                    var vramTotal = toGiB(stats.vramTotalBytes).toFixed(1);
                    footerVram.textContent = "MODEL VRAM  " + vramUsed + " / " + vramTotal + " GB";
                    footerVramBar.style.setProperty("--progress", (stats.vramUsedBytes / stats.vramTotalBytes * 100) + "%");
                } else {
                    footerVram.textContent = "MODEL VRAM  n.b.";
                    footerVramBar.style.setProperty("--progress", "0%");
                }
            })
            .catch(function () {
                // Transient read failure - keep showing the last known values.
            });
    }

    function pollTokenSummary() {
        fetch(apiBaseUrl + "/api/token-summary")
            .then(function (r) { return r.json(); })
            .then(function (summary) {
                headerTokensTotal.textContent = formatTokens(summary.totalTokens);
                headerTokensRequests.textContent = formatTokens(summary.requestCount);
                footerRequests.textContent = "AI-VERZOEKEN  " + formatTokens(summary.requestCount);
            })
            .catch(function () {
                // Transient read failure - keep showing the last known values.
            });
    }

    pollSystemStats();
    pollTokenSummary();
    setInterval(pollSystemStats, 5000);
    setInterval(pollTokenSummary, 20000);
})();
