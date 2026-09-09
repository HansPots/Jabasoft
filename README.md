# Jabasoft

De launchershell van de JabaSoft-familie: de app die je opstart en
vanwaaruit je de andere JabaSoft-apps start.

## Architectuur

Herbouwd op de uniforme WPF-stack (zie `Jabasoft.Stylebook`'s
geschiedenis voor waarom). `Jabasoft.App` is een losse WPF-executable,
geen webhost, geen embedding: een klik op een geregistreerde app start
die app's eigen `.exe` als apart proces/venster. Draait die al, dan
komt het bestaande venster naar voren in plaats van een tweede
exemplaar te starten.

De lijst met apps staat in `appsettings.json` (`Apps[]`: `Name`,
`DisplayName`, `ExecutablePath`, `Available`). Een app met
`Available: false` staat zichtbaar maar niet-klikbaar in het menu -
"nog niet herbouwd" op de nieuwe stack.

Styling komt van `Jabasoft.Stylebook/Stylebook.Components` (gedeelde
`{DynamicResource}`-tokens/`Theme.xaml`), zodat Jabasoft er visueel
consistent uitziet met de rest van de familie.

## Status

v1: alleen de launcher. Geen token-dashboard, geen instellingenscherm,
geen `Jabasoft.Base`/`Jabasoft.Broker`-integratie - die komen terug
zodra de AI-gerelateerde apps (te beginnen met `LocalAiStudio`) zelf
weer op de nieuwe stack bestaan.
