# Provider Fixtures

No file in this folder contains captured provider data.

`gdacs-earthquakes.json` is a small synthetic file with the structure of the
official GDACS `geteventlist/SEARCH` GeoJSON response. Every ID, place name,
coordinate and date in it is made up. The second feature exercises optional null
metadata. Automated tests never contact GDACS.

`reliefweb-reports.json` is a small synthetic ReliefWeb V2-shaped metadata fixture created for deterministic parser and client tests. It contains no copied report body, files, images, or attachments; the placeholder `body` and `file` fields exist only to verify that unsupported content is ignored.

`ucdp-candidate-sample.csv` is a small synthetic file with the exact column
header of the UCDP Candidate Events monthly CSV release (`GEDEvent_v26_0_8.csv`
header as observed on 2026-10-02). Every row, name and number in it is made up
for parser tests; it contains no real UCDP records, and automated tests never
contact UCDP. Line breaks, commas and quotes inside quoted fields, a
country-level row, a zero-fatality row, a malformed id and a short row are
included on purpose.
