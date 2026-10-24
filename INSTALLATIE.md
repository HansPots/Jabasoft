# Jabasoft installeren op een nieuwe pc

Alles wat nodig is om de JabaSoft-familie (Jabasoft, Stylebook, AI Studio, TabStudio, ImageFunn) op een
nieuwe Windows-pc te laten draaien. Opgesteld op 12-10-2026 vanaf de werkende pc; de versienummers zijn wat
daar draait en getest is.

> **Belangrijk:** de apps verwijzen naar elkaar via vaste paden onder `D:\Repos\...` (zie de
> `appsettings.json` van elke app). Zet de repo's dus precies daar neer, met precies deze mapnamen.

---

## 1. Hardware

| | Minimaal | Getest |
|---|---|---|
| Besturingssysteem | Windows 11 64-bit | Windows 11 Pro |
| Werkgeheugen | 32 GB (ImageFunn-video laadt pieken van ~24 GB) | 32 GB |
| Videokaart | NVIDIA met 12 GB VRAM (alleen nodig voor ImageFunn en lokale AI) | RTX 4070 SUPER, 12 GB, driver 617.42 |
| Vrije schijfruimte | ~250 GB | modellen: Hugging Face ~150 GB, Ollama ~75 GB |

Zonder NVIDIA-kaart werken Jabasoft, Stylebook en TabStudio wel (TabStudio rekent op de CPU), maar
ImageFunn niet, en lokale AI-modellen worden erg traag.

---

## 2. Software installeren

Installeer in deze volgorde. De `winget`-commando's zijn voor een terminal (PowerShell); bij een ✱ even
controleren met `winget search <naam>` of de id nog klopt.

### Basis (altijd nodig)

| Software | Waarvoor | Installeren | Getest |
|---|---|---|---|
| Git | repo's ophalen | `winget install Git.Git` | 2.55 |
| .NET 10 SDK | alle apps bouwen (net10.0-windows) | `winget install Microsoft.DotNet.SDK.10` | 10.0.401 |
| WebView2 Runtime | tab-weergave (AlphaTab) en AI Studio | zit in Windows 11; anders `winget install Microsoft.EdgeWebView2Runtime` | 154 |
| SQL Server 2025 Express LocalDB | databases | komt mee met Visual Studio 2026 (workload "Gegevensopslag en -verwerking"), of los via de SQL Server 2025 Express-installer (*Download Media* > **LocalDB**); instantie **(localdb)\MSSQLLocalDB**, Windows-authenticatie | 17.0 |
| ODBC Driver 18 for SQL Server | TabStudio (Python, pyodbc) | ✱ `winget install Microsoft.msodbcsql.18` | 18 |
| sqlcmd | backups terugzetten, scripts | komt mee met de SQL Server-clienttools / ODBC-tools | 18 (ODBC) |

### Voor TabStudio en ImageFunn

| Software | Waarvoor | Installeren | Getest |
|---|---|---|---|
| Python 3.11 | audio-service (TabStudio) en beeldservice (ImageFunn) | `winget install Python.Python.3.11` | 3.11.9 |
| FFmpeg | audio/video omzetten | `winget install Gyan.FFmpeg` | 9.0.1 |
| Deno | yt-dlp heeft het nodig voor sommige YouTube-video's | `winget install DenoLand.Deno` | - |
| NVIDIA-driver | ImageFunn (PyTorch met CUDA 12.4) | via GeForce Experience / nvidia.com | 617.42 |

### Voor de AI-functies (broker, AI Studio, token-overzicht)

De broker praat met een lokale AI-server. Installeer Ollama, LM Studio, of allebei.

| Software | Installeren | Opmerking |
|---|---|---|
| Ollama | `winget install Ollama.Ollama` | poort 11434; modellen ophalen met `ollama pull ...` (zie hieronder) |
| LM Studio | ✱ `winget install ElementLabs.LMStudio` | poort 1234; server aanzetten in LM Studio |

Ollama-modellen die op de oude pc stonden (haal op wat je gebruikt):

```
ollama pull qwen2.5-coder:14b
ollama pull qwen2.5-coder:7b
ollama pull qwen2.5-coder:1.5b
ollama pull qwen3:14b
ollama pull qwen3-vl:8b
ollama pull gemma3:12b
ollama pull deepseek-r1:14b
ollama pull qwen2.5:32b
ollama pull nomic-embed-text-v2-moe
```

### Handig, niet verplicht

| Software | Waarvoor | Installeren |
|---|---|---|
| Visual Studio 2026 Community | ontwikkelen (workload ".NET-desktopontwikkeling") | ✱ `winget install Microsoft.VisualStudio.Community` |
| SQL Server Management Studio 22 | databases bekijken, backups | ✱ `winget install Microsoft.SQLServerManagementStudio` |
| Node.js | alleen de meetlat-controle `meetlat/check_alphatex.mjs` | `winget install OpenJS.NodeJS.LTS` |
| GitHub CLI | repo's aanmaken vanaf de commandoregel | `winget install GitHub.cli` |

---

## 3. Repo's ophalen

Alles komt onder `D:\Repos`:

```
mkdir D:\Repos
cd D:\Repos
git clone -b development https://github.com/HansPots/Jabasoft.git
git clone -b development https://github.com/HansPots/Jabasoft.Base.git
git clone -b development https://github.com/HansPots/Jabasoft.Broker.git
git clone -b development https://github.com/HansPots/Jabasoft.Stylebook.git
git clone -b development https://github.com/HansPots/JabaSoft.LocalAiStudio.git
git clone -b master      https://github.com/HansPots/JabaSoft.TabStudio.git
git clone -b master      https://github.com/HansPots/JabaSoft.ImageFunn.git
```

De solution (`Jabasoft.slnx`), het startscript en deze handleiding staan in de Jabasoft-repo zelf.

`D:\Repos\TestApp` staat niet op GitHub; zet hem alleen over (bijv. via OneDrive) als je hem gebruikt.

---

## 4. Databases terugzetten

De apps gebruiken vier databases in LocalDB, op `(localdb)\MSSQLLocalDB` (Windows-authenticatie). Dat staat
in de connectiestrings van `Jabasoft.Broker\appsettings.json`, `Jabasoft.Stylebook\Stylebook.Playground\appsettings.json`
en `JabaSoft.TabStudio\TabStudio.Audio\correcties.py`. Een gewone SQL Server-instantie (`localhost`) wordt
niet meer gebruikt.

| Database | Gebruikt door |
|---|---|
| `JabasoftBase` | broker (tokenverbruik) |
| `JabasoftAi` | broker (AI-instellingen, zoekdocumenten) |
| `JabasoftStylebook` | Stylebook |
| `JabaSoftTabStudio` | TabStudio (correcties, extra noten, leerdata, metingen) |

**Aanbevolen: de backups terugzetten.** Die staan in
`C:\Users\macpo\OneDrive\Documenten\SQL Server Management Studio 22\Code Snippets\SQL\backup`
(bijv. `JabaSoftTabStudio_20261012_2119.bak`).

1. Kopieer de `.bak`-bestanden naar een map die LocalDB mag lezen, bijv. `C:\Users\Public\Documents\SqlBackup`.
2. Zet ze terug (in SSMS verbinden met `(localdb)\MSSQLLocalDB`, dan *Databases > Restore Database*; of met
   `sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i terugzetten.sql`). Pas de datum in de namen en `<gebruiker>` aan -
   LocalDB zet zijn databasebestanden standaard in je gebruikersmap:

```sql
RESTORE DATABASE JabasoftBase FROM DISK = N'C:\Users\Public\Documents\SqlBackup\JabasoftBase_20261012_2119.bak'
  WITH MOVE 'JabasoftBase' TO N'C:\Users\<gebruiker>\JabasoftBase.mdf',
       MOVE 'JabasoftBase_log' TO N'C:\Users\<gebruiker>\JabasoftBase_log.ldf';
RESTORE DATABASE JabasoftAi FROM DISK = N'C:\Users\Public\Documents\SqlBackup\JabasoftAi_20261012_2119.bak'
  WITH MOVE 'JabasoftAi' TO N'C:\Users\<gebruiker>\JabasoftAi.mdf',
       MOVE 'JabasoftAi_log' TO N'C:\Users\<gebruiker>\JabasoftAi_log.ldf';
RESTORE DATABASE JabasoftStylebook FROM DISK = N'C:\Users\Public\Documents\SqlBackup\JabasoftStylebook_20261012_2119.bak'
  WITH MOVE 'JabasoftStylebook' TO N'C:\Users\<gebruiker>\JabasoftStylebook.mdf',
       MOVE 'JabasoftStylebook_log' TO N'C:\Users\<gebruiker>\JabasoftStylebook_log.ldf';
RESTORE DATABASE JabaSoftTabStudio FROM DISK = N'C:\Users\Public\Documents\SqlBackup\JabaSoftTabStudio_20261012_2119.bak'
  WITH MOVE 'JabaSoftTabStudio' TO N'C:\Users\<gebruiker>\JabaSoftTabStudio.mdf',
       MOVE 'JabaSoftTabStudio_log' TO N'C:\Users\<gebruiker>\JabaSoftTabStudio_log.ldf';
```

(LocalDB nog niet gestart? `sqllocaldb start MSSQLLocalDB`. Bestaat de instantie niet:
`sqllocaldb create MSSQLLocalDB -s`.)

**Zonder backup (helemaal leeg beginnen):** `JabasoftAi` maakt de broker zelf aan bij de eerste start en
`JabasoftStylebook` de Stylebook-app; `JabaSoftTabStudio` maak je aan met de scripts in
`JabaSoft.TabStudio\TabStudio.Audio\vergelijking\` (`maak_correcties_tabel.sql`, `maak_extranoten_tabel.sql`,
`maak_leerdata_tabellen.sql`, `maak_meting_tabellen.sql`). Voor `JabasoftBase` is een backup het makkelijkst.

---

## 5. Python-omgevingen

Elke service heeft een eigen `.venv`; de apps starten die zelf (de paden staan vast in de code).

### TabStudio-audio (`D:\Repos\JabaSoft.TabStudio\TabStudio.Audio`)

```
cd D:\Repos\JabaSoft.TabStudio\TabStudio.Audio
py -3.11 -m venv .venv
.venv\Scripts\python.exe -m pip install -r requirements.txt
```

Draait op de CPU (geen CUDA nodig). Bij het eerste gebruik worden modellen opgehaald: Demucs
(`htdemucs_6s`), CREPE, het GAPS-gitaarmodel (Hugging Face) en Beat This.

### ImageFunn-beeld (`D:\Repos\JabaSoft.ImageFunn\ImageFunn.Image`)

```
cd D:\Repos\JabaSoft.ImageFunn\ImageFunn.Image
py -3.11 -m venv .venv
.venv\Scripts\python.exe -m pip install torch torchvision --index-url https://download.pytorch.org/whl/cu124
.venv\Scripts\python.exe -m pip install -r requirements.txt
```

De beeld- en videomodellen (~150 GB) halen: met de `download_*.py`-scripts in die map, of ze worden bij het
eerste gebruik opgehaald. Zet eerst `set HF_HUB_DISABLE_XET=1` - anders gaat het downloaden erg traag.

---

## 6. Bouwen en starten

```
D:\Repos\Jabasoft\Start-Jabasoft.cmd
```

Dit bouwt de Jabasoft-solution (`Jabasoft.slnx`: Jabasoft, Base, Broker, Stylebook, AI Studio, TabStudio,
ImageFunn) en start Jabasoft. In Visual Studio open je dezelfde `Jabasoft.slnx`, met Jabasoft.App als startproject.

AI Studio, TabStudio en ImageFunn zijn geen losse programma's meer: het zijn onderdelen van Jabasoft en
openen in hetzelfde venster, met hun eigen menu (de Home-knop brengt je terug). Alleen Stylebook start
nog als eigen programma. De broker en de Python-services starten automatisch zodra je een onderdeel opent.

### Poorten (als iets niet bereikbaar is)

| Poort | Dienst |
|---|---|
| 5310 | AI-broker (Jabasoft.Broker) |
| 5320 | TabStudio audio-service |
| 5330 | ImageFunn beeldservice |
| 1234 | LM Studio |
| 11434 | Ollama |

---

## 7. Wat niet in git zit (eventueel overzetten)

Niets hiervan is nodig om te starten, maar zonder begin je op die punten opnieuw:

| Wat | Waar | Zonder overzetten |
|---|---|---|
| TabStudio-bibliotheek (gemaakte tabs) | `JabaSoft.TabStudio\TabStudio.Audio\jobs\` | bibliotheek leeg, én de Meting-pagina mist de 12 meetlat-licks (die gebruiken de herkende noten uit `jobs\`) - **overzetten aanbevolen** |
| Leerdata-video's en uitgelezen tabs | `JabaSoft.TabStudio\TabStudio.Audio\leerdata\` | de database weet nog welke video's er waren, maar het trainen heeft de bestanden nodig - opnieuw laten verwerken |
| Snaarkeuze-model | `JabaSoft.TabStudio\TabStudio.Audio\modellen\snaarkeuze.pt` | opnieuw trainen met de knop op de Leerdata-pagina (een paar minuten) |
| Meetlat-video's en cache | `JabaSoft.TabStudio\TabStudio.Audio\meetlat\*.mp4` en `meetlat\cache\` (de juiste antwoorden, `gt_*.json`, staan wél in git) | alleen nodig voor de oude meetlat-scripts; de Meting-pagina gebruikt `jobs\` |
| ImageFunn-uitvoer (beelden, video's) | `JabaSoft.ImageFunn\ImageFunn.Image\uitvoer\` | galerij leeg |
| Hugging Face-modellen | `%USERPROFILE%\.cache\huggingface` | opnieuw downloaden (~150 GB) |
| Ollama-modellen | `%USERPROFILE%\.ollama\models` | `ollama pull ...` |

---

## 8. Controlelijst

- [ ] Git, .NET 10 SDK, SQL Server 2025 Express LocalDB, ODBC Driver 18 geïnstalleerd
- [ ] Python 3.11, FFmpeg, Deno geïnstalleerd (`ffmpeg -version` werkt in een nieuwe terminal)
- [ ] NVIDIA-driver (voor ImageFunn), Ollama en/of LM Studio met modellen
- [ ] Alle zeven repo's gekloond in `D:\Repos`
- [ ] Vier databases teruggezet in `(localdb)\MSSQLLocalDB`
- [ ] Beide `.venv`'s aangemaakt en pakketten geïnstalleerd
- [ ] `Start-Jabasoft.cmd` bouwt zonder fouten en Jabasoft start
- [ ] AI Studio, TabStudio en ImageFunn vanuit het Jabasoft-menu te openen
