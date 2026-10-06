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

The app reads the key from the `GEOPOLITICALMONITOR_FIRMS_MAP_KEY` environment variable once, at startup. It never stores the key, shows it or passes it to the user interface. A key set or changed later takes effect only after a restart.

**Recommended: for one PowerShell session.** Set the variable in the window you start the app from. It disappears when that window closes.

```powershell
$env:GEOPOLITICALMONITOR_FIRMS_MAP_KEY = "<your MAP_KEY>"
& ".\src\backend\GeopoliticalMonitor.Desktop\bin\Debug\net10.0-windows10.0.19041.0\win-x64\GeopoliticalMonitor.Desktop.exe"
```

**Option: as a user-level environment variable.** This keeps the key between sessions, but it stores the key unencrypted in your user environment. Other programs running as your user can read it there.

```powershell
[Environment]::SetEnvironmentVariable("GEOPOLITICALMONITOR_FIRMS_MAP_KEY", "<your MAP_KEY>", "User")
```

The value reaches programs started after it was set, so open a new PowerShell window before launching. To remove it:

```powershell
[Environment]::SetEnvironmentVariable("GEOPOLITICALMONITOR_FIRMS_MAP_KEY", $null, "User")
```

**Without a key,** everything else works. `Refresh FIRMS` shows `NASA FIRMS is not configured.` and sends no request.

Never paste the key into documents, issues or chat.

## Other data sources

- **UCDP Candidate:** no key or account. The required citation, funding acknowledgement and CC BY 4.0 license are shown in the app (Settings, Data sources, and the Details of each UCDP Event) and in [data sources](data-sources.md#ucdp-candidate).
- **GDACS:** no key or account.
- **ReliefWeb:** dormant by owner decision. No setup is needed, and the app makes no ReliefWeb requests.

## X login

The X pane shows the normal X website. To see your own feed, sign in on the website inside the pane. The app never receives your password. Your session is ordinary browser state in the X pane's own profile folder (see Data locations), and the app does not read it. `Clear Session` in the X pane's `...` menu deletes all cookies and clears all browsing data of that profile, which signs you out of X. Clear Session clears the X pane's browsing data; check the ad blocker status afterwards.

## AdBlock (optional)

The X pane can host the AdBlock browser extension in its own profile. The app does not download, bundle or silently install it. Only one reviewed version is accepted: Chrome Web Store AdBlock `6.47.0`, extension id `gighmmpiobklfepjocnamgkkbiglidom`, with a fixed content digest. A newer version needs a separately reviewed trust record in the code before it can be installed.

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

### Other dialog buttons

- **`Enable` and `Disable`:** turn the installed ad blocker on and off.
- **`Remove`:** removes it.
- **`Test Extension Support`:** a separate diagnostic. It does not repair or install the production ad blocker.

Do not use `Remove` unless you intend to remove the ad blocker, or `Clear Session` unless you intend to sign out of X.

## Data locations

All locations come from the app's path provider. The folder name `GeopoliticalMonitor` is deliberate and stays the same whatever the app's display name becomes, so a rename never moves your data. Close the app before changing anything here.

| Location under `%LOCALAPPDATA%\GeopoliticalMonitor` | Contents | Deleting it |
|---|---|---|
| `Data\geopolitical-monitor.db` | The database: Events, Sources, Watchlists and memberships, Alert rules and history, preferences (accent, X pane side and width) | Removes all of that. The next launch creates an empty database, and providers fill it again from their next refreshes. Watchlists, Alerts and attached X posts are lost. |
| `Data\x-adblock-binding.json` | The link between the reviewed AdBlock copy and the installed instance | Do not delete by hand. Use the ad blocker dialog instead. |
| `Data\diagnostic-adblock-binding.json` | State for the `Test Extension Support` diagnostic | Diagnostic only. |
| `Extensions\AdBlock\Current` (and the `Current.staging` and `Current.previous` folders during an update) | The verified AdBlock copy the X pane loads | Do not delete by hand. Use `Remove` in the ad blocker dialog. |
| `WebView2\AppShell` | Browser data of the app's own interface view | Contains no Events or settings; it is browser data only. |
| `WebView2\XBrowser` | The X pane's browser profile: X sign-in, X website data and the installed ad blocker instance | Signs you out of X and removes the installed ad blocker instance. Prefer `Clear Session` for signing out. |
| `WebView2\ExtensionCapabilityDiagnostic` | Profile used only by `Test Extension Support` | Diagnostic only. |
| Backup folders you create, for example `Data-backup-<date>` | Your copies of the `Data` folder | The app never reads them. |

## Backing up

1. Close the app.
2. Copy the whole `%LOCALAPPDATA%\GeopoliticalMonitor\Data` folder to a backup folder, for example `%LOCALAPPDATA%\GeopoliticalMonitor\Data-backup-<date>`.

Make a backup before running a build that changes the database schema. A backed-up database works only with a build whose schema matches it or is newer. A newer build upgrades the database at startup, and an older build cannot open an upgraded database. To restore, close the app and copy the backup back over `Data`.

## Troubleshooting

- **Blank window:** the frontend was not built before the desktop project. Run `npm run build` in `src/frontend`, then rebuild the desktop project.
- **Slow first launch:** a cold first launch can be slow; launches right after it are fast. This is known and not an error.
- **FIRMS messages:**
  - `NASA FIRMS is not configured.`: no key was set when the app started. See [NASA FIRMS](#nasa-firms).
  - `Zoom in to a smaller area before refreshing NASA FIRMS.`: the visible area is wider than 90 degrees of longitude or taller than 60 degrees of latitude.
  - `NASA FIRMS returned too much data for this area. Zoom in and refresh again.` and `Too many FIRMS detections for this area. Zoom in and refresh again.`: the area has too many detections; zoom in.
  - `NASA FIRMS refresh failed. Existing layer data was preserved.`: the request failed; the points already shown stay.
- **Ad blocker status line:** see the [user guide](user-guide.md#x-pane) for what each line means.
- **Minimum width:** the main area (Events and Map) never gets narrower than 940 px.
- **X pane cut off at the right edge:** below about 1,270 px of window width the X pane can be clipped. This is a known open issue with no fix planned yet.
