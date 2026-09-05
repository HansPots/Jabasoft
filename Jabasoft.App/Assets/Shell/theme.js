/* Shared by every Jabasoft-local page (shell.html, dashboard.html,
   styleguide.html, loading.html): applies the theme choice stored by the
   header's toggle to this page's own <html> element, so a full page
   navigation (e.g. the content iframe reloading) doesn't silently reset
   back to the default LCARS look. Local-only mechanism - never reaches
   into TabStudio/LocalAiStudio's own, separately-rendered pages. */
(function () {
    "use strict";

    var STORAGE_KEY = "jabasoft-theme";

    function applyStoredTheme() {
        var theme = null;
        try {
            theme = localStorage.getItem(STORAGE_KEY);
        } catch (e) {
            // localStorage unavailable (e.g. blocked) - just keep the default theme.
        }

        if (theme === "vs") {
            document.documentElement.setAttribute("data-theme", "vs");
        } else {
            document.documentElement.removeAttribute("data-theme");
        }
    }

    function setTheme(theme) {
        try {
            localStorage.setItem(STORAGE_KEY, theme);
        } catch (e) {
            // ignore - theme just won't persist across pages this session
        }
        applyStoredTheme();
    }

    window.jabasoftTheme = { apply: applyStoredTheme, set: setTheme };
    applyStoredTheme();
})();
