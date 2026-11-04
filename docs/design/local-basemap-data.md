# Local Basemap Data

Task 003 renders the world map with MapLibre GL JS and bundled local geography.

Dataset:

- Package: `world-atlas` 2.0.2
- File used: `countries-110m.json`
- Source: Natural Earth Admin 0 country boundaries and land geometry, 1:110m small scale
- Format: TopoJSON converted to GeoJSON in the frontend bundle with `topojson-client`
- Runtime behavior: bundled static asset/module only; no remote tile provider, style URL, geocoding provider, or map server

Provenance and licensing:

- `world-atlas` describes itself as pre-built TopoJSON from Natural Earth vector data.
- Natural Earth states that its vector and raster map data are public domain and may be used, modified, and redistributed without permission or required attribution.
- The package code is ISC licensed.

Current limitations:

- Country labels are not rendered in Task 003 because the selected 110m dataset is used primarily for land and country boundaries.
- Conflict zones and tension tints come from the owner-curated reference file `src/frontend/src/data/conflictTensionLayer.json`; conflict polygons are approximate, and tension entries reuse these bundled country shapes (see `docs/design/conflict-tension-layer-design.md`).
- Event markers are mock/demo frontend data using longitude/latitude. Live event ingestion and final backend geographic models remain future work.
