(function () {
    "use strict";

    var config = window.jabasoftConfig || { apiBaseUrl: "http://localhost:5300" };
    var rows = document.getElementById("rows");
    var empty = document.getElementById("empty");

    fetch(config.apiBaseUrl + "/api/token-usage")
        .then(function (response) {
            if (!response.ok) {
                throw new Error("HTTP " + response.status);
            }
            return response.json();
        })
        .then(function (entries) {
            if (!entries || entries.length === 0) {
                empty.textContent = "Nog geen geregistreerd tokenverbruik.";
                empty.style.display = "block";
                return;
            }

            entries
                .slice()
                .sort(function (a, b) { return new Date(b.timestamp) - new Date(a.timestamp); })
                .forEach(function (entry) {
                    var tr = document.createElement("tr");
                    tr.innerHTML =
                        "<td>" + entry.application + "</td>" +
                        "<td>" + new Date(entry.timestamp).toLocaleString() + "</td>" +
                        "<td>" + (entry.model || "-") + "</td>" +
                        "<td>" + entry.promptTokens + "</td>" +
                        "<td>" + entry.completionTokens + "</td>" +
                        "<td>" + entry.totalTokens + "</td>";
                    rows.appendChild(tr);
                });
        })
        .catch(function (err) {
            empty.textContent = "Kon tokenverbruik niet laden: " + err;
            empty.style.display = "block";
        });
})();
