# Setup and configuration

This guide covers building the app from source, starting it, configuring the optional parts, where it keeps its data, and how to back that up. Using the app is described in the [user guide](user-guide.md).

## Prerequisites

| Item | Version or setting | Where it is defined |
|---|---|---|
| Operating system | Windows on x64 (`win-x64`), platform version `10.0.17763.0` or later | `src/backend/GeopoliticalMonitor.Desktop/GeopoliticalMonitor.Desktop.csproj` |
| .NET | An SDK that builds `net10.0` (desktop project: `net10.0-windows10.0.19041.0`); no exact SDK version is pinned | Project files; there is no `global.json` |
| Windows App SDK | `Microsoft.WindowsAppSDK` 1.8.251003001, self-contained (`WindowsAppSDKSelfContained`), unpackaged (`WindowsPackageType` `None`) | Desktop project file |
| WebView2 | `Microsoft.Web.WebView2` 1.0.3179.45; the app uses the WebView2 Runtime installed in Windows | Desktop project file, `MainWindow.xaml.cs` |
| Node.js and npm | No version is pinned (no `engines` field and no `.nvmrc`); `package-lock.json` is lockfile version 3 | `src/frontend/package.json`, `src/frontend/package-lock.json` |
| NuGet source | `nuget.org` only | `nuget.config` |

No account, API key or developer access is needed to build or run the app. The NASA FIRMS key is optional.

### Tested with

This is what the project was built and tested with. It is not a pinned requirement.

- .NET SDK 10.0.400. Other SDKs were also installed; the repository has no `global.json`, so the newest installed SDK is used.
- Node.js v26.10.0 with npm 11.19.1.
- Windows 10 build 19045 (version 10.0.19045).

## Building and running

The build order matters. The desktop project copies the files in `src/frontend/dist` into `wwwroot` in its output folder, and the app loads its interface from there. If the frontend was not built first, `wwwroot` stays empty and the window is blank. After rebuilding the frontend, rebuild the desktop project too.

1. Build and test the frontend:

   ```powershell
   cd src/frontend
   npm ci
   npm test -- --run
   npm run build
   cd ../..
   ```

   `npm ci` is needed only when `node_modules` is missing or `package-lock.json` changed. `npm run build` type-checks and writes `src/frontend/dist`.

2. Build and test the .NET solution:

   ```powershell
   dotnet restore src/backend/GeopoliticalMonitor.sln
   dotnet build src/backend/GeopoliticalMonitor.sln --no-restore --no-incremental -p:UseSharedCompilation=false
   dotnet test src/backend/GeopoliticalMonitor.sln --no-restore
   ```

   The solution contains the four application projects and the three test projects (Domain, Application, Infrastructure). A full non-incremental build is the reliable way to see every analyzer warning; the expected result is zero warnings. To run one test project:

   ```powershell
   dotnet test tests/GeopoliticalMonitor.Application.Tests --no-restore
   ```

3. Start the app from the repository root:

   ```powershell
   & ".\src\backend\GeopoliticalMonitor.Desktop\bin\Debug\net10.0-windows10.0.19041.0\win-x64\GeopoliticalMonitor.Desktop.exe"
   ```

The executable keeps the name `GeopoliticalMonitor.Desktop.exe`; its file properties (Details tab) show the product name and file description `Northern Monitor`.

While the app is running, its executable and build output are locked, so the desktop project cannot be rebuilt. Close the app first, or build and test the other projects individually.

`npm run dev` starts the Vite development server on `127.0.0.1:5173` for working on the interface. It is development tooling only; the app does not use it.

## First launch

- The app creates its folders under `%LOCALAPPDATA%\GeopoliticalMonitor` and creates or upgrades its database.
- The app no longer contains demo data. Fictional demo Events from older versions (ids starting `evt-demo-`) and their demo Sources (`src-demo-`) are removed once, and a one-time notice says `Demo Events were removed.`
- With an empty database the Events list is empty at first. The first GDACS refresh starts 60 seconds after the window is ready, and the first UCDP check 2 minutes after. Both then repeat while the app is open: GDACS every 30 minutes and UCDP every 6 hours.
- The Events list starts at the `30D` range and `High and above` severity. UCDP data is about 20 to 50 days old, so after the first UCDP import most UCDP Events do not appear at `30D`; choose `90D` or `ALL` to see them.
- No provider is contacted before the window is ready. The app makes no request when it is closed.

## NASA FIRMS

The FIRMS fire and thermal layer needs a MAP_KEY from NASA. NASA's [MAP_KEY page](https://firms.modaps.eosdis.nasa.gov/api/map_key/) describes the key as free. You request it with your email address, and it is sent by email. NASA states a limit of 5000 transactions per 10-minute interval, and larger requests may count as more than one. The [FIRMS API page](https://firms.modaps.eosdis.nasa.gov/api/) lists the other services.

There are two ways to give the app the key. If both are set, the key set in the app is used.

**Recommended: set it in the app.** Open Settings and go to Data sources. Under `NASA FIRMS key`, choose `Set key...`. A Windows dialog opens; paste the key into its password box and choose `Save`. The status then shows `Configured (set in app)`. The key takes effect at the next `Refresh FIRMS`, without a restart. `Remove key` deletes only the key stored in the app; after that the environment variable below is used, if it is set.

- **Format check:** a NASA FIRMS key is normally 32 hexadecimal characters. If the text you enter is not, the dialog warns you, and you can still save it. Settings then shows the warning, and NASA may reject the key.
- **Where it is stored:** in Windows Credential Manager, as a generic credential for your Windows account on this computer (it does not roam to other computers). Windows protects it with your account, but other programs running as your Windows user can still read it. It is not a vault.
- **Never in the app's data:** the key is not written to the database, the `Data` folder, its backups, logs or the user interface. Settings shows only key status: whether a key is configured, where it comes from, whether its format looks valid, whether the credential store could be reached, and the result of your last set or remove. The app has no way to show the stored key again.

**Fallback: the environment variable.** The app also reads `GEOPOLITICALMONITOR_FIRMS_MAP_KEY` from its own process environment, each time you refresh FIRMS. A program's environment is fixed when it starts, so a variable set or changed while the app is running takes effect only after the app is started again from a window that has it. Spaces and one pair of surrounding quotes are removed from the value, as in the app dialog.

To avoid typing the key into a terminal where others can see it, copy the key from where you keep it, then set the variable from the clipboard and clear the clipboard. For one PowerShell session only (the variable disappears when that window closes):

```powershell
$env:GEOPOLITICALMONITOR_FIRMS_MAP_KEY = (Get-Clipboard -Raw).Trim()
Set-Clipboard -Value $null
& ".\src\backend\GeopoliticalMonitor.Desktop\bin\Debug\net10.0-windows10.0.19041.0\win-x64\GeopoliticalMonitor.Desktop.exe"
```

As a user-level environment variable, which keeps the key between sessions but stores it unencrypted in your user environment, where other programs running as your user can read it:

```powershell
[Environment]::SetEnvironmentVariable("GEOPOLITICALMONITOR_FIRMS_MAP_KEY", (Get-Clipboard -Raw).Trim(), "User")
Set-Clipboard -Value $null
```

The value reaches programs started after it was set, so open a new PowerShell window before launching. To remove it:

```powershell
[Environment]::SetEnvironmentVariable("GEOPOLITICALMONITOR_FIRMS_MAP_KEY", $null, "User")
```

**Without a key,** everything else works. `Refresh FIRMS` shows `NASA FIRMS is not configured. Set a key in Settings, Data sources.` and sends no request.

Never paste the key into documents, issues or chat.

## Other data sources

- **UCDP Candidate:** no key or account. The required citation, funding acknowledgement and CC BY 4.0 license are shown in the app (Settings, Data sources, and the Details of each UCDP Event) and in [data sources](data-sources.md#ucdp-candidate).
- **GDACS:** no key or account.
- **ReliefWeb:** dormant by owner decision. No setup is needed, and the app makes no ReliefWeb requests.

## X and Bluesky login

The social pane shows the normal X website or the normal Bluesky website; choose one with the `X` and `Bluesky` buttons in the pane header. To see your own feed, sign in on the website inside the pane. The app never receives your password. Each website's session is ordinary browser state in its own profile folder (`WebView2\XBrowser` for X, `WebView2\BlueskyBrowser` for Bluesky; see Data locations), and the app does not read it. `Clear Session` in the `...` menu deletes all cookies and clears all browsing data of the profile of the website shown, which signs you out of that website only. Clear Session clears that pane's browsing data; check its ad blocker status afterwards.

The Bluesky profile folder is created the first time you choose `Bluesky`; until then the app starts no Bluesky browser and sends no Bluesky request. Only top-level pages on `bsky.app` open in the Bluesky pane. If Bluesky's sign-in opens a page on another address, that page opens in your system browser instead.

## AdBlock (optional)

The X pane can host the AdBlock browser extension in its own profile, and the Bluesky pane can host its own, separately verified copy in its profile. The app does not download, bundle or silently install it. Only one reviewed version is accepted: Chrome Web Store AdBlock `6.47.0`, extension id `gighmmpiobklfepjocnamgkkbiglidom`, with a fixed content digest. A newer version needs a separately reviewed trust record in the code before it can be installed.

### Install or re-enroll

Re-enroll when ads reappear in the X pane and the ad blocker dialog shows `Installed: No` and `Managed: No`, with a message that the bound instance is no longer installed. The stored copy and its binding can still be valid in that state; installing again restores the instance.

1. In the X pane, open the `...` menu (More X actions) and choose `Ad Blocker...`.
2. In the `X Ad Blocker` dialog choose `Install AdBlock...`.
3. Select the AdBlock version folder that directly contains `manifest.json`, for example Chrome's unpacked copy:

   ```text
   %LOCALAPPDATA%\Google\Chrome\User Data\Default\Extensions\gighmmpiobklfepjocnamgkkbiglidom\6.47.0_0
   ```

   This path assumes Chrome's `Default` profile. If you use several Chrome profiles, the folder may sit under a different profile folder name instead of `Default`, for example `Profile 1`. Select the version folder itself, the one that directly contains `manifest.json`, not the `gighmmpiobklfepjocnamgkkbiglidom` folder above it. Chrome's `_metadata` folder inside it is ignored by the check.

4. If the dialog's message asks for a restart, close and reopen the app. Then open the dialog again and choose `Reconcile`, or `Install AdBlock...` if no instance remains.

Keep an unpacked copy of the `6.47.0` folder outside Chrome's profile, because Chrome may delete old version folders after it updates the extension.

### Restore the trusted instance (X)

In one earlier browser profile the ad blocker was observed to be missing after the application was restarted; the cause was not established. `Restore trusted instance` remains available if the extension is ever missing. In that state the dialog shows `Installed: No`, `Managed: No` and says the bound instance is no longer installed.

1. In the X pane, open the `...` menu and choose `Ad Blocker...`.
2. Choose `Restore trusted instance`. The app checks the binding, authenticates the verified copy again against the reviewed `6.47.0` record and adds the ad blocker from that copy. No folder is chosen and nothing is written into the copy.
3. The dialog should then show `Installed: Yes` and `Managed: Yes`.

The action appears only when a valid X binding and a verified copy exist and the bound instance is missing. If the copy fails the check, nothing is changed and the dialog says so; use `Install AdBlock...` with the reviewed folder instead. The app never restores the ad blocker on its own, and the Bluesky dialog has no restore action.

### Install for Bluesky

The Bluesky pane has no ad blocker unless you install it there. It uses its own copy of the same reviewed `6.47.0` payload, its own binding and its own status line. Installing, removing or changing it never touches the X ad blocker; the X copy is only read.

1. Choose `Bluesky` in the pane header, open the `...` menu and choose `Ad Blocker...`.
2. In the `Bluesky Ad Blocker` dialog choose one source:
   - `Install AdBlock...` and select the reviewed `6.47.0` version folder, as for X; or
   - `Use verified X copy`, which authenticates the X pane's managed copy again in full and copies it into the Bluesky folder. It is offered only when the X copy exists, and nothing is copied if the X copy does not match the reviewed record.
3. The first installation for Bluesky always needs a restart, because the Bluesky pane starts without extensions until a verified Bluesky copy exists. Close and reopen the app, choose `Bluesky`, open the dialog again and choose `Install AdBlock...` or `Use verified X copy` again (the existing verified Bluesky copy is reused), or `Reconcile` when it is offered.

Each time the Bluesky pane is created, the app authenticates the Bluesky copy in full before the first Bluesky page loads. A copy that fails the check keeps the Bluesky pane without extensions for that run; X is not affected.

### Other dialog buttons

- **`Enable` and `Disable`:** turn the installed ad blocker on and off.
- **`Remove`:** removes it.
- **`Test Extension Support`:** a separate diagnostic, only in the `X Ad Blocker` dialog. It does not repair or install the production ad blocker.

In the `Bluesky Ad Blocker` dialog these buttons act on the Bluesky copy only; its `Remove` deletes only the Bluesky copy and binding.

Do not use `Remove` unless you intend to remove that pane's ad blocker, or `Clear Session` unless you intend to sign out of the website shown.

## Data locations

All locations come from the app's path provider. The folder name `GeopoliticalMonitor` is deliberate and stays the same whatever the app's display name becomes, so a rename never moves your data. Close the app before changing anything here.

| Location under `%LOCALAPPDATA%\GeopoliticalMonitor` | Contents | Deleting it |
|---|---|---|
| `Data\geopolitical-monitor.db` | The database: Events, Sources, Watchlists and memberships, Alert rules and history, preferences (accent, pane side and width, and `SocialPaneFeed`, the selected website `X` or `Bluesky`) | Removes all of that. The next launch creates an empty database, and providers fill it again from their next refreshes. Watchlists, Alerts and attached X posts are lost. |
| `Data\x-adblock-binding.json` | The link between the reviewed AdBlock copy and the installed instance | Do not delete by hand. Use the ad blocker dialog instead. |
| `Data\bluesky-adblock-binding.json` | The link between the Bluesky pane's verified AdBlock copy and its installed instance; exists only after you install AdBlock for Bluesky | Do not delete by hand. Use the `Bluesky Ad Blocker` dialog instead. X is not affected either way. |
| `Data\diagnostic-adblock-binding.json` | State for the `Test Extension Support` diagnostic | Diagnostic only. |
| `Extensions\AdBlock\Current` (and the `Current.staging` and `Current.previous` folders during an update) | The verified AdBlock copy the X pane loads | Do not delete by hand. Use `Remove` in the ad blocker dialog. |
| `Extensions\AdBlockBluesky\Current` (and its own `Current.staging` and `Current.previous` folders during an update) | The Bluesky pane's own verified AdBlock copy; exists only after you install AdBlock for Bluesky | Do not delete by hand. Use `Remove` in the `Bluesky Ad Blocker` dialog. Deleting it leaves the Bluesky pane without its ad blocker; X is not affected. |
| `WebView2\AppShell` | Browser data of the app's own interface view | Contains no Events or settings; it is browser data only. |
| `WebView2\XBrowser` | The X pane's browser profile: X sign-in, X website data and the installed ad blocker instance | Signs you out of X and removes the installed ad blocker instance. Prefer `Clear Session` for signing out. |
| `WebView2\BlueskyBrowser` | The Bluesky pane's browser profile: Bluesky sign-in, Bluesky website data and, if installed, its ad blocker instance; created the first time you choose `Bluesky` | Signs you out of Bluesky and removes its installed ad blocker instance. Prefer `Clear Session` for signing out. X is not affected. |
| `WebView2\ExtensionCapabilityDiagnostic` | Profile used only by `Test Extension Support` | Diagnostic only. |
| `Diagnostics` | An old folder from earlier builds, if present; no longer used | Can be deleted manually. |
| Backup folders you create, for example `Data-backup-<date>` | Your copies of the `Data` folder | The app never reads them. |

## Backing up

1. Close the app.
2. Copy the whole `%LOCALAPPDATA%\GeopoliticalMonitor\Data` folder to a backup folder, for example `%LOCALAPPDATA%\GeopoliticalMonitor\Data-backup-<date>`.

The `Data` folder holds the database, including the `SocialPaneFeed` preference, and both ad blocker binding files. The verified AdBlock copies under `Extensions` are not in it; they can be installed again from the reviewed folder.

Make a backup before running a build that changes the database schema. A backed-up database works only with a build whose schema matches it or is newer. A newer build upgrades the database at startup, and an older build cannot open an upgraded database. To restore, close the app and copy the backup back over `Data`.

## Troubleshooting

- **Blank window:** the frontend was not built before the desktop project. Run `npm run build` in `src/frontend`, then rebuild the desktop project.
- **Slow first launch:** a cold first launch can be slow; launches right after it are fast. This is known and not an error.
- **FIRMS messages:**
  - `NASA FIRMS is not configured. Set a key in Settings, Data sources.`: no key is set in the app and the environment variable is not set for the running app. See [NASA FIRMS](#nasa-firms).
  - `NASA FIRMS rejected the key. ...`: NASA answered that the key is invalid. Check the key in Settings, Data sources, or set it again. If the message also says the key is not 32 hexadecimal characters, the key in use has the wrong format.
  - `NASA FIRMS reported too many requests for this key. ...`: NASA answered with HTTP 429 (too many requests). Wait a few minutes and refresh again.
  - `NASA FIRMS did not respond in time. ...`: no answer within 15 seconds. Refresh again later.
  - `NASA FIRMS did not accept a requested VIIRS product. ...`: NASA answered that it does not accept the product name the app requested. Refresh again later.
  - `Zoom in to a smaller area before refreshing NASA FIRMS.`: the visible area is wider than 90 degrees of longitude or taller than 60 degrees of latitude.
  - `NASA FIRMS returned too much data for this area. Zoom in and refresh again.` and `Too many FIRMS detections for this area. Zoom in and refresh again.`: the area has too many detections; zoom in.
  - `NASA FIRMS refresh failed. Existing layer data was preserved.`: the request failed; the points already shown stay.
- **Ad blocker status line:** see the [user guide](user-guide.md#ad-blocker-status-lines) for what each line means.
- **X ad blocker gone after a restart:** if the `X Ad Blocker` dialog says the bound instance is no longer installed, use `Restore trusted instance` (see [Restore the trusted instance](#restore-the-trusted-instance-x)).
- **A Bluesky page does not load:** the pane shows `Bluesky website could not load` with `Retry Bluesky`. Choose `Retry Bluesky`, or `Open External` in the `...` menu to open the page in your system browser. A link to an address other than `bsky.app` always opens in your system browser, never in the pane.
- **Minimum width:** the main area (Events and Map) never gets narrower than 940 px.
- **Social pane cut off at the right edge:** below about 1,270 px of window width the X or Bluesky pane can be clipped. This is a known open issue with no fix planned yet.
