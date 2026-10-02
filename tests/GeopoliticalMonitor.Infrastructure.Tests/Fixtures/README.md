# Provider Fixtures

`gdacs-earthquakes.json` is a reduced recording of the official GDACS
`geteventlist/SEARCH` GeoJSON schema. Its first feature was captured from the
official earthquake query for 2025-09-16 through 2025-09-18 and then reduced to
the fields used by this integration. The second feature is a schema-consistent
minimal record used to exercise optional null metadata. It contains no secrets
and automated tests never contact GDACS.

`reliefweb-reports.json` is a small synthetic ReliefWeb V2-shaped metadata fixture created for deterministic parser and client tests. It contains no copied report body, files, images, or attachments; the placeholder `body` and `file` fields exist only to verify that unsupported content is ignored.

`ucdp-candidate-sample.csv` is a small synthetic file with the exact column
header of the UCDP Candidate Events monthly CSV release (`GEDEvent_v26_0_8.csv`
header as observed on 2026-10-02). Every row, name and number in it is made up
for parser tests; it contains no real UCDP records, and automated tests never
contact UCDP. Line breaks, commas and quotes inside quoted fields, a
country-level row, a zero-fatality row, a malformed id and a short row are
included on purpose.
