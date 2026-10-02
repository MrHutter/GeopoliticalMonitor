# Third-Party Notices

This file lists third-party components used by the Geopolitical Monitor project, with the version and license identifier recorded for each. Licenses are taken from the package metadata (npm `package-lock.json`, NuGet `.nuspec`) and from the project's basemap data documentation. Refer to each component's own license text for the full terms.

## npm production dependencies (frontend runtime)

Production (non-dev) packages from `src/frontend/package-lock.json`, the dependency tree from which the bundled frontend is built. Development-only tooling (build, test and dev type packages) is not listed here.

| Package | Version | License |
| :- | :- | :- |
| @mapbox/jsonlint-lines-primitives | 2.0.3 | MIT |
| @mapbox/point-geometry | 1.1.0 | ISC |
| @mapbox/tiny-sdf | 2.2.0 | BSD-2-Clause |
| @mapbox/unitbezier | 1.0.0 | BSD-2-Clause |
| @mapbox/vector-tile | 3.0.0 | BSD-3-Clause |
| @maplibre/geojson-vt | 6.1.1 | ISC |
| @maplibre/maplibre-gl-style-spec | 26.4.4 | ISC |
| @maplibre/mlt | 1.3.0 | (MIT OR Apache-2.0) |
| @maplibre/vt-pbf | 4.3.2 | MIT |
| @types/geojson | 7946.0.16 | MIT |
| bidi-js | 1.1.0 | MIT |
| commander | 2.20.3 | MIT |
| earcut | 3.2.3 | ISC |
| gl-matrix | 3.4.4 | MIT |
| json-stringify-pretty-compact | 4.0.0 | MIT |
| kdbush | 4.1.0 | ISC |
| maplibre-gl | 6.10.0 | BSD-3-Clause |
| minimist | 1.2.8 | MIT |
| murmurhash-js | 1.0.0 | MIT |
| pbf | 5.1.2 | BSD-3-Clause |
| potpack | 2.1.0 | ISC |
| protocol-buffers-schema | 3.6.1 | MIT |
| quickselect | 3.0.0 | ISC |
| react | 19.3.0 | MIT |
| react-dom | 19.3.0 | MIT |
| require-from-string | 2.0.2 | MIT |
| resolve-protobuf-schema | 2.1.0 | MIT |
| scheduler | 0.28.0 | MIT |
| tinyqueue | 3.0.0 | ISC |
| topojson-client | 3.1.0 | ISC |
| world-atlas | 2.0.2 | ISC |

## NuGet packages

Direct and transitive packages resolved for the projects under `src/` and `tests/`. "Used by" shows which projects resolve the package: Desktop (the application), Infrastructure (including build and design-time tooling), and Tests (test projects only).

| Package | Version | License | Used by |
| :- | :- | :- | :- |
| coverlet.collector | 6.0.4 | MIT | Tests |
| Humanizer.Core | 2.14.1 | MIT | Infrastructure |
| Microsoft.Build.Framework | 18.0.2 | MIT | Infrastructure |
| Microsoft.CodeAnalysis.Analyzers | 3.11.0 | MIT | Infrastructure |
| Microsoft.CodeAnalysis.Common | 5.0.0 | MIT | Infrastructure |
| Microsoft.CodeAnalysis.CSharp | 5.0.0 | MIT | Infrastructure |
| Microsoft.CodeAnalysis.CSharp.Workspaces | 5.0.0 | MIT | Infrastructure |
| Microsoft.CodeAnalysis.Workspaces.Common | 5.0.0 | MIT | Infrastructure |
| Microsoft.CodeAnalysis.Workspaces.MSBuild | 5.0.0 | MIT | Infrastructure |
| Microsoft.CodeCoverage | 17.14.1 | MIT | Tests |
| Microsoft.Data.Sqlite.Core | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.EntityFrameworkCore | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.EntityFrameworkCore.Abstractions | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.EntityFrameworkCore.Analyzers | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 | MIT | Infrastructure |
| Microsoft.EntityFrameworkCore.Relational | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.EntityFrameworkCore.Sqlite.Core | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.Extensions.Caching.Abstractions | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.Extensions.Caching.Memory | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.Extensions.DependencyInjection | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.Extensions.DependencyModel | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.Extensions.Logging | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.Extensions.Options | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.Extensions.Primitives | 10.0.12 | MIT | Desktop, Infrastructure, Tests |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | Tests |
| Microsoft.TestPlatform.ObjectModel | 17.14.1 | MIT | Tests |
| Microsoft.TestPlatform.TestHost | 17.14.1 | MIT | Tests |
| Microsoft.VisualStudio.SolutionPersistence | 1.0.52 | MIT | Infrastructure |
| Microsoft.Web.WebView2 | 1.0.3179.45 | Custom Microsoft license (BSD-style; `LICENSE.txt` in package) | Desktop |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.4654 | Not determined (package declares only a license URL: https://aka.ms/WinSDKLicenseURL) | Desktop |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.20250829.1 | Microsoft Software License Terms, Windows SDK (`sdk_license.txt` in package) | Desktop |
| Microsoft.WindowsAppSDK | 1.8.251003001 | Microsoft Software License Terms, Windows App SDK (`license.txt` in package) | Desktop |
| Microsoft.WindowsAppSDK.AI | 1.8.38 | Microsoft Software License Terms, Windows App SDK (`license.txt` in package) | Desktop |
| Microsoft.WindowsAppSDK.Base | 1.8.250831001 | Microsoft Software License Terms, Windows App SDK (`license.txt` in package) | Desktop |
| Microsoft.WindowsAppSDK.DWrite | 1.8.25090401 | Microsoft Software License Terms, Windows App SDK (`license.txt` in package) | Desktop |
| Microsoft.WindowsAppSDK.Foundation | 1.8.250906002 | Microsoft Software License Terms, Windows App SDK (`license.txt` in package) | Desktop |
| Microsoft.WindowsAppSDK.InteractiveExperiences | 1.8.250906004 | Microsoft Software License Terms, Windows App SDK (`license.txt` in package) | Desktop |
| Microsoft.WindowsAppSDK.ML | 1.8.2095 | Microsoft Software License Terms, Windows App SDK (`license.txt` in package) | Desktop |
| Microsoft.WindowsAppSDK.Runtime | 1.8.251003001 | Microsoft Software License Terms, Windows App SDK (`license.txt` in package) | Desktop |
| Microsoft.WindowsAppSDK.Widgets | 1.8.250904007 | Microsoft Software License Terms, Windows App SDK (`license.txt` in package) | Desktop |
| Microsoft.WindowsAppSDK.WinUI | 1.8.250906003 | Microsoft Software License Terms, Windows App SDK (`license.txt` in package) | Desktop |
| Mono.TextTemplating | 3.0.0 | MIT | Infrastructure |
| Newtonsoft.Json | 13.0.3 | MIT | Tests |
| Newtonsoft.Json | 13.0.4 | MIT | Infrastructure |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.12 | Apache-2.0 | Desktop, Infrastructure, Tests |
| SQLitePCLRaw.core | 2.1.12 | Apache-2.0 | Desktop, Infrastructure, Tests |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.12 | Apache-2.0 | Desktop, Infrastructure, Tests |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.12 | Apache-2.0 | Desktop, Infrastructure, Tests |
| System.CodeDom | 6.0.0 | MIT | Infrastructure |
| System.Composition | 9.0.0 | MIT | Infrastructure |
| System.Composition.AttributedModel | 9.0.0 | MIT | Infrastructure |
| System.Composition.Convention | 9.0.0 | MIT | Infrastructure |
| System.Composition.Hosting | 9.0.0 | MIT | Infrastructure |
| System.Composition.Runtime | 9.0.0 | MIT | Infrastructure |
| System.Composition.TypedParts | 9.0.0 | MIT | Infrastructure |
| xunit | 2.9.3 | Apache-2.0 | Tests |
| xunit.abstractions | 2.0.3 | Not determined (package declares only a license URL: https://raw.githubusercontent.com/xunit/xunit/master/license.txt) | Tests |
| xunit.analyzers | 1.18.0 | Apache-2.0 | Tests |
| xunit.assert | 2.9.3 | Apache-2.0 | Tests |
| xunit.core | 2.9.3 | Apache-2.0 | Tests |
| xunit.extensibility.core | 2.9.3 | Apache-2.0 | Tests |
| xunit.extensibility.execution | 2.9.3 | Apache-2.0 | Tests |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | Tests |

## Map data

| Component | Version | License |
| :- | :- | :- |
| world-atlas (`countries-110m.json`, pre-built TopoJSON) | 2.0.2 | ISC |
| Natural Earth Admin 0 country boundaries and land, 1:110m | Distributed via world-atlas 2.0.2 | Public domain |

See `docs/design/local-basemap-data.md` for provenance details.

## Datasets

| Dataset | Use | License |
| :- | :- | :- |
| UCDP Candidate Events Dataset, monthly releases (Uppsala Conflict Data Program) | Downloaded at runtime from `https://ucdp.uu.se/downloads/` while the app is open; not bundled | CC BY 4.0 |

UCDP citation: Hegre, Håvard, Mihai Croicu, Kristine Eck, and Stina Högbladh (July 2020). Introducing the UCDP Candidate Events Dataset. Research & Politics. doi:10.1177/2053168020935257. UCDP is part of and funded by DEMSCORE, national research infrastructure grant 2021-00162 from the Swedish Research Council.
