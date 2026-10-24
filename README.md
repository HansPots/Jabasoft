# Jabasoft

De launchershell van de JabaSoft-familie: de app die je opstart en
vanwaaruit je de andere JabaSoft-apps start.

## Architectuur

Herbouwd op de uniforme WPF-stack (zie `Jabasoft.Stylebook`'s
geschiedenis voor waarom). `Jabasoft.App` is één WPF-applicatie met één
venster. AI Studio (`JabaSoft.LocalAiStudio`), TabStudio en ImageFunn zijn
bibliotheken waar Jabasoft naar verwijst: een klik in het menu zet de
complete paginaschil van dat onderdeel (eigen header, eigen menu, eigen
footer) in hetzelfde venster, en de Home-knop van het onderdeel zet het
scherm van Jabasoft terug. Een onderdeel wordt bij de eerste klik gemaakt
(en start dan zijn Python-service) en daarna hergebruikt.

Alleen Stylebook (het ontwerpgereedschap) is nog een eigen programma; het
pad staat in `appsettings.json` (`Apps[]`).

Styling komt van `Jabasoft.Stylebook/Stylebook.Components` (gedeelde
`{DynamicResource}`-tokens/`Theme.xaml`), zodat Jabasoft er visueel
consistent uitziet met de rest van de familie.

## Status

v1: alleen de launcher. Geen token-dashboard, geen instellingenscherm,
geen `Jabasoft.Base`/`Jabasoft.Broker`-integratie - die komen terug
zodra de AI-gerelateerde apps (te beginnen met `LocalAiStudio`) zelf
weer op de nieuwe stack bestaan.
