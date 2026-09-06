(function () {
    "use strict";

    var config = window.jabasoftConfig || { apps: {} };
    var nav = document.getElementById("nav");
    var content = document.getElementById("content");
    var shell = document.querySelector(".shell");
    var items = [];

    // First in the menu, always - what a "jabasoft:home" message (posted
    // by the embedded app's own Home nav item, see the message listener
    // below) returns to.
    items.push({ label: "Home", url: "home.html" });

    Object.keys(config.apps || {}).forEach(function (key) {
        var app = config.apps[key];
        var url = app.mainUrl || app.developmentUrl;
        if (url) {
            // "focus": embedded apps get the ENTIRE window while active
            // (see .shell.app-focus in shell.css) - no Jabasoft header,
            // menu, or action rail at all. The app itself adds a "Home"
            // item to its own menu (shown only when embedded - see
            // Shared.UI/wwwroot/embed.js) that posts a message back here.
            items.push({ label: app.displayName || key, url: url, focus: true });
        }
    });

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
        var buttons = nav.querySelectorAll("button");
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].classList.toggle("active", i === index);
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
        var button = document.createElement("button");
        button.type = "button";
        button.textContent = item.label;
        button.addEventListener("click", function () {
            activate(index);
        });
        nav.appendChild(button);
    });

    if (items.length > 0) {
        activate(0);
    }

    // The embedded app's own "Home" nav item (see Shared.UI/wwwroot/
    // embed.js) posts this instead of navigating anywhere itself, since
    // Jabasoft's own menu is completely hidden while an app has focus -
    // there's nothing on this side for the user to click.
    window.addEventListener("message", function (event) {
        if (event.data === "jabasoft:home") {
            activate(0);
        }
    });

    // A <select> rather than a toggle button: LCARS/VS today, but this is
    // meant to grow - each new theme (see Jabasoft.Stylebook/Shared.UI/
    // wwwroot) is just another <option> in shell.html, no JS changes.
    var themeSelect = document.getElementById("theme-select");
    if (themeSelect && window.jabasoftTheme) {
        themeSelect.value = document.documentElement.getAttribute("data-theme") || "lcars";
        themeSelect.addEventListener("change", function () {
            window.jabasoftTheme.set(themeSelect.value);

            // Reload whatever's currently shown (styleguide.html or a
            // directly-embedded app) so it re-reads the new theme
            // immediately instead of only on next navigation. No-op if
            // the token dashboard (a different control) is what's
            // actually visible right now.
            content.src = content.src;
        });
    }
})();
