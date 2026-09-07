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

    function activate(index) {
        activeIndex = index;

        var buttons = nav.querySelectorAll(".nav-btn");
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].classList.toggle("active", Number(buttons[i].dataset.index) === index);
        }

        var item = items[index];
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
})();
