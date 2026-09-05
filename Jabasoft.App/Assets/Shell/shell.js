(function () {
    "use strict";

    var config = window.jabasoftConfig || { apps: {} };
    var nav = document.getElementById("nav");
    var content = document.getElementById("content");
    var items = [];

    Object.keys(config.apps || {}).forEach(function (key) {
        var app = config.apps[key];
        var url = app.mainUrl || app.developmentUrl;
        if (url) {
            items.push({ label: app.displayName || key, url: url });
        }
    });

    items.push({ label: "Token verbruik", url: "dashboard.html" });
    items.push({ label: "Stijlgids", url: "styleguide.html" });

    function activate(index) {
        var buttons = nav.querySelectorAll("button");
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].classList.toggle("active", i === index);
        }
        content.src = items[index].url;
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

    var themeToggle = document.getElementById("theme-toggle");
    if (themeToggle && window.jabasoftTheme) {
        themeToggle.addEventListener("click", function () {
            var current = document.documentElement.getAttribute("data-theme");
            window.jabasoftTheme.set(current === "vs" ? "lcars" : "vs");

            // Reload whatever's currently shown (dashboard.html,
            // styleguide.html, or a directly-embedded app) so it re-reads
            // the new theme immediately instead of only on next navigation.
            content.src = content.src;
        });
    }
})();
