import type { FeatureCollection, Point } from 'geojson';
import type { FirmsObservation } from '../../nativeBridge';

export const firmsSourceId = 'nasa-firms-observations';
export const firmsLayerId = 'nasa-firms-observations-circle';

export type FirmsDisplayMode = 'significant' | 'strong' | 'all';

export const firmsDisplayThresholds = {
  significantFrpMw: 10,
  strongFrpMw: 50,
} as const;

export const firmsDisplayModeOptions: ReadonlyArray<{ value: FirmsDisplayMode; label: string }> = [
  { value: 'significant', label: 'Significant' },
  { value: 'strong', label: 'Strong only' },
  { value: 'all', label: 'All detections' },
];

export function filterFirmsObservations(
  observations: readonly FirmsObservation[],
  mode: FirmsDisplayMode,
) {
  if (mode === 'all') {
    return [...observations];
  }

  const minimumFrp = mode === 'significant'
    ? firmsDisplayThresholds.significantFrpMw
    : firmsDisplayThresholds.strongFrpMw;
  return observations.filter((observation) => (
    observation.confidence === 'Nominal' || observation.confidence === 'High'
  ) && observation.frp >= minimumFrp);
}

export const firmsAgeBuckets = ['under-3h', '3-6h', '6-12h', '12-24h'] as const;
export type FirmsAgeBucket = (typeof firmsAgeBuckets)[number];

export type FirmsFeatureProperties = Omit<FirmsObservation, 'latitude' | 'longitude'> & {
  ageBucket: FirmsAgeBucket;
};

export function classifyFirmsAge(acquiredAtUtc: string, referenceUtc: string): FirmsAgeBucket {
  const ageHours = Math.max(0, (Date.parse(referenceUtc) - Date.parse(acquiredAtUtc)) / 3_600_000);
  if (ageHours < 3) return 'under-3h';
  if (ageHours < 6) return '3-6h';
  if (ageHours < 12) return '6-12h';
  return '12-24h';
}

export function firmsObservationsToFeatureCollection(
  observations: FirmsObservation[],
  referenceUtc: string | null,
): FeatureCollection<Point, FirmsFeatureProperties> {
  const effectiveReference = referenceUtc ?? new Date(0).toISOString();
  return {
    type: 'FeatureCollection',
    features: observations.map((observation) => ({
      type: 'Feature',
      id: observation.id,
      properties: {
        id: observation.id,
        acquiredAtUtc: observation.acquiredAtUtc,
        source: observation.source,
        satellite: observation.satellite,
        confidence: observation.confidence,
        frp: observation.frp,
        brightnessTemperatureI4: observation.brightnessTemperatureI4,
        brightnessTemperatureI5: observation.brightnessTemperatureI5,
        scan: observation.scan,
        track: observation.track,
        processingVersion: observation.processingVersion,
        dayNight: observation.dayNight,
        ageBucket: classifyFirmsAge(observation.acquiredAtUtc, effectiveReference),
      },
      geometry: { type: 'Point', coordinates: [observation.longitude, observation.latitude] },
    })),
  };
}

export const firmsCirclePaint = {
  'circle-color': [
    'match', ['get', 'ageBucket'],
    'under-3h', '#ff9a3d',
    '3-6h', '#d77a36',
    '6-12h', '#9f6040',
    '12-24h', '#6f4c43',
    '#6f4c43',
  ],
  'circle-opacity': [
    'match', ['get', 'ageBucket'],
    'under-3h', 0.92,
    '3-6h', 0.78,
    '6-12h', 0.62,
    '12-24h', 0.44,
    0.44,
  ],
  'circle-radius': 3.5,
  'circle-stroke-color': '#17120f',
  'circle-stroke-width': 0.8,
} as const;

function escapeHtml(value: unknown) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

export function firmsPopupHtml(properties: Record<string, unknown>, longitude: number, latitude: number) {
  const sourceLabel = properties.source === 'VIIRS_NOAA20_NRT' ? 'NOAA-20' : 'NOAA-21';
  const acquired = new Date(String(properties.acquiredAtUtc));
  const acquiredText = Number.isFinite(acquired.getTime()) ? `${acquired.toISOString().replace('T', ' ').slice(0, 16)} UTC` : '';
  const frp = typeof properties.frp === 'number' ? `${properties.frp.toFixed(1)} MW` : '';
  return [
    '<strong>NASA FIRMS — Fire / Thermal Anomaly</strong>',
    `<span>${escapeHtml(acquiredText)}</span>`,
    `<span>${sourceLabel} · confidence ${escapeHtml(properties.confidence)}</span>`,
    frp ? `<span>FRP ${escapeHtml(frp)} · ${escapeHtml(properties.dayNight)}</span>` : '',
    `<span>${latitude.toFixed(4)}, ${longitude.toFixed(4)} · version ${escapeHtml(properties.processingVersion)}</span>`,
    '<small>NASA FIRMS / LANCE</small>',
  ].filter(Boolean).join('');
}
