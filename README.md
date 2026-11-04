# Northern Monitor

Northern Monitor is a Windows desktop application for geopolitical situational awareness and open-source research. It keeps a local database of Events from public data sources, shows them in a list and on a bundled world map, and places the normal X website or the normal Bluesky website, switched by you, in a separate pane beside them.

## Status

- Early-stage personal project in a private repository.
- No license has been chosen yet. The display name is Northern Monitor; the code, the data folder and the executable keep the identifier `GeopoliticalMonitor`.
- There is no installer. The app is built from source and started from its build output.

## Requirements

- Windows on x64 (`win-x64`), platform version `10.0.17763.0` or later. The desktop project targets `net10.0-windows10.0.19041.0`.
- A .NET SDK that can build `net10.0` projects. The repository does not pin an exact SDK version.
- Node.js with npm, to build the frontend. The repository does not pin a Node.js version.
- The WebView2 Runtime installed in Windows. The app does not bundle a fixed-version runtime.
- The versions the project was built and tested with are listed under [Tested with](docs/guide/setup-and-configuration.md#tested-with).

Exact package versions and the full build steps are in [the setup guide](docs/guide/setup-and-configuration.md).

## Quick start

Build the frontend first. The desktop build copies `src/frontend/dist` into its output, so a desktop build made without it opens a blank window.

```powershell
cd src/frontend
npm ci
npm test -- --run
npm run build
cd ../..
dotnet build src/backend/GeopoliticalMonitor.sln
dotnet test src/backend/GeopoliticalMonitor.sln
```

Start the app from the repository root:

```powershell
& ".\src\backend\GeopoliticalMonitor.Desktop\bin\Debug\net10.0-windows10.0.19041.0\win-x64\GeopoliticalMonitor.Desktop.exe"
```

The app runs only while its window is open. Closing the window ends it.

## NASA FIRMS key

The fire and thermal anomaly layer needs a free NASA FIRMS MAP_KEY. Set it in the app under Settings, Data sources, with `Set key...`: a Windows dialog stores it in Windows Credential Manager for your Windows account. The `GEOPOLITICALMONITOR_FIRMS_MAP_KEY` environment variable still works as a fallback. Without a key everything else works, and Refresh FIRMS reports that NASA FIRMS is not configured without sending a request. See [NASA FIRMS](docs/guide/setup-and-configuration.md#nasa-firms) in the setup guide.

## Data sources

| Source | What it adds | Needs |
|---|---|---|
| GDACS | Earthquake Events | Nothing; refreshed while the app is open |
| UCDP Candidate | Monthly conflict Events, about 20 to 50 days old | Nothing; checked while the app is open |
| NASA FIRMS | Transient fire and thermal points for the visible map area, never stored | A free MAP_KEY |
| X | Posts you attach to an Event by pasting their URL | Your own X login in the X pane |
| Conflict and tension layer | Owner-curated reference areas on the map; all entries are drafts | Nothing; bundled |
| Natural Earth basemap | The bundled world map | Nothing; bundled |
| ReliefWeb | Not active (dormant by owner decision) | Nothing to set up |

Details, freshness, attribution and limits: [data sources](docs/guide/data-sources.md).

## Privacy and security

- Everything runs locally. Events, Sources, Watchlists, Alerts and preferences are stored in a SQLite database under `%LOCALAPPDATA%\GeopoliticalMonitor`.
- The app opens no inbound network ports. It installs no Windows service, scheduled task or startup entry, and leaves no background process running after its window closes.
- Provider requests happen only while the app is open, on its in-app timer or when you ask for them.
- The social pane shows the normal X website or the normal Bluesky website, each in its own isolated browser view with its own profile. Neither has a connection to the app's internal message bridge, and the app does not scrape, read or inject anything into X or Bluesky pages. Nothing from Bluesky is attached to Events.
- The FIRMS key is entered only in a Windows dialog and stored only in Windows Credential Manager, or read from the environment variable. It is never shown again, never sent to the user interface, and never written to the database, logs or backups of the `Data` folder.

Vulnerability reports: [SECURITY.md](SECURITY.md). Third-party components and data licenses: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Troubleshooting

- **Blank window:** the frontend was not built before the desktop project. Run `npm run build` in `src/frontend`, then rebuild the desktop project.
- **Slow first launch:** a cold first launch can be slow; launches right after it are fast. This is known and not an error.
- **Social pane cut off at the right edge:** below about 1,270 px of window width the X or Bluesky pane can be clipped. This is a known open issue.

More in [the setup guide](docs/guide/setup-and-configuration.md#troubleshooting).

## Re-enrolling AdBlock

The steps for installing or re-enrolling the optional ad blocker in the X pane, and for installing it separately for the Bluesky pane, are in [the setup guide](docs/guide/setup-and-configuration.md#adblock-optional).

## Documentation

- [User guide](docs/guide/user-guide.md): using the app.
- [Setup and configuration](docs/guide/setup-and-configuration.md): building, configuring, data locations and backups.
- [Data sources](docs/guide/data-sources.md): what each source provides and its terms.
- [Documentation index](docs/README.md): every document, its audience and what it covers.
