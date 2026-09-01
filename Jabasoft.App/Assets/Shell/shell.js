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
})();
