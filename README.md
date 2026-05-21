# Gbx Map Browser
<h3>The Gbx Map Browser helps you to browse and explore your '.gbx' map and replay files with ease.</h3>

![image](https://user-images.githubusercontent.com/74670743/132136258-e1d2ec46-d5bf-40c8-af94-067435d58177.png)


It's based on gbx.net libraries: https://github.com/BigBang1112/gbx-net

It uses MahApps Metro UI: https://github.com/MahApps/MahApps.Metro

It uses Octokit: https://github.com/octokit/octokit.net


<h2>Download</h2>

Download the latest Windows release from the GitHub **Releases** page.

Download the `.zip` file, extract it, then run:

```text
GbxMapBrowser.exe
```

Windows may show a SmartScreen warning because the app is not code-signed yet. Click **More info** and then **Run anyway** if you trust the download.


<h2>Features</h2>

- launch a map and select by which game
- rename a map (inside metadata or file), delete a map
- search for a map
- sort maps - by name, date, size, length, medal, ...
- drag and drop a map to other folder/app (eg. TMX) or into this program
- drop a map on the program's exe and it will show the map preview directly
- see all map properties in Gbx Preview feature
- use games' shortcuts to navigate directly into your maps
- edit settings for a TrackMania / ShootMania game - change map folder, change launched exe location, change icon, ...
- full keyboard navigation and shortcuts
- open current folder in windows file explorer
- show system file dialog
- custom titlepacks support
- check for update on start
- set a default map folder
- move downloaded `.Map.Gbx` files from your Downloads folder into your default map folder
- show Trackmania personal bests and medals in the map list
- read local Trackmania replay personal bests
- save PB and medal data locally
- detect your Trackmania/Nadeo Account ID from Openplanet logs
- use dedicated server credentials to check missing PBs from Trackmania online records
- cache online “not found” results so the same maps are not checked repeatedly


<h2>Showcase</h2>

<img src="https://github.com/ArkadySK/GbxMapBrowser/assets/74670743/25eb08b2-899f-48aa-a226-0358cd1e0f8a" data-canonical-src="https://github.com/ArkadySK/GbxMapBrowser/assets/74670743/25eb08b2-899f-48aa-a226-0358cd1e0f8a" height="600" />

<img src="https://github.com/ArkadySK/GbxMapBrowser/assets/74670743/5554bd29-384c-4226-b3b8-edac24add8e2" data-canonical-src="https://github.com/ArkadySK/GbxMapBrowser/assets/74670743/5554bd29-384c-4226-b3b8-edac24add8e2" height="600" />



<h2>Trackmania PB and Medal Detection</h2>

When you click **Refresh PB**, the app uses this order:

1. Scan local Trackmania replay files first.
2. Scan the current map folder for map UIDs and medal times.
3. Update all PBs that can be found locally.
4. For maps that still have no PB, check the online cache.
5. Skip maps already marked as online `NotFound`.
6. Check the remaining missing maps from Trackmania online records.
7. Save online PBs locally if they are found.
8. Mark maps as `NotFound` if no online PB exists, so they are not checked again next time.

Local replay PBs always win. Online records are only used to fill missing PBs.


<h2>Online PB Setup</h2>

Online PB lookup is optional. The app does **not** need your Ubisoft password.

Click the globe/online button in the app to open **Trackmania Online Setup**.

<h3>Step 1: Trackmania Account ID</h3>

The app needs your Trackmania/Nadeo Account ID.

You can either:

- paste the Account ID manually, or
- detect it automatically from Openplanet logs.

To use automatic detection:

1. Install Openplanet: https://openplanet.dev/download
2. Launch Trackmania once with Openplanet installed.
3. Close Trackmania.
4. Reopen the setup page in GbxMapBrowser.
5. Click **Find my Account ID automatically**.
6. Click **Save Account ID**.

The Account ID is saved locally here:

```text
%LOCALAPPDATA%\GbxMapBrowser\trackmania-account-id.txt
```

<h3>Step 2: Dedicated server credentials</h3>

To check online records, the app needs a Trackmania dedicated server login and password.

Do **not** enter your Ubisoft password.

Create a separate dedicated server account:

1. Open the Trackmania server account page: https://www.trackmania.com/player/dedicated-servers
2. Sign in with your Ubisoft/Trackmania account in the browser.
3. On the **MY SERVER ACCOUNTS** page, type a server name in the **Server Login** field.
4. Click **Submit**.
5. The page will generate a password in red text next to the server name.
6. Copy the server name you typed.
7. Paste it into **Dedicated server login / server name** in the app.
8. Copy the generated red password.
9. Paste it into **Generated dedicated server password** in the app.
10. Click **Save dedicated server credentials**.

The dedicated server credentials are stored locally and encrypted for your Windows user account.

They are saved here:

```text
%LOCALAPPDATA%\GbxMapBrowser\trackmania-dedicated-server-credentials.dat
```


<h2>Local Data Files</h2>

The app stores local data under:

```text
%LOCALAPPDATA%\GbxMapBrowser
```

Important files:

- `trackmania-records.json` — local PB and medal database.
- `trackmania-online-record-cache.json` — online lookup cache, including `Found`, `NotFound`, and temporary `Failed` states.
- `trackmania-account-id.txt` — saved Trackmania/Nadeo Account ID.
- `trackmania-dedicated-server-credentials.dat` — encrypted dedicated server credentials.
- `default-map-folder.txt` — saved default map folder.


<h2>Requirements</h2>

**Operating System:** Windows 8.1, Windows 10 or Windows 11

**Have atleast one game installed from this list:** TrackMania United Forever, TrackMania Nations Forever, ManiaPlanet, TrackMania Turbo or TrackMania2020 (TMNext)

**Optional for automatic Account ID detection:** Openplanet

**Optional for online PB lookup:** Trackmania dedicated server account credentials


<h2>Build From Source</h2>

Build:

```powershell
cd "D:\GbxMapBrowserwithtimeing"
dotnet build ".\GbxMapBrowser.sln"
```

Run from source:

```powershell
cd "D:\GbxMapBrowserwithtimeing\GbxMapBrowser"
dotnet run
```

Create a Windows release build:

```powershell
cd "D:\GbxMapBrowserwithtimeing"

dotnet publish ".\GbxMapBrowser\GbxMapBrowser.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o ".\release\GbxMapBrowser-win-x64"
```


<h2>Privacy and Security Notes</h2>

- The app does not ask for your Ubisoft password.
- The app does not use OAuth or ship a ClientSecret.
- Dedicated server credentials are stored locally and encrypted with Windows Data Protection for the current Windows user.
- Online lookup only runs when you click **Refresh PB**.
- Online `NotFound` results are cached to avoid repeatedly checking the same maps.


<h2>Credits</h2>

Based on the original Gbx Map Browser project by ArkadySK.

Uses:

- GBX.NET: https://github.com/BigBang1112/gbx-net
- MahApps.Metro: https://github.com/MahApps/MahApps.Metro
- Octokit: https://github.com/octokit/octokit.net
