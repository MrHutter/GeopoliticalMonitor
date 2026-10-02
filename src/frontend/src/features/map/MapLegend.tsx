import { eventSeverities } from '../../types/events';
import { conflictLegendLabel, conflictZoneColors, tensionColors, tensionLegendLabel } from './conflictTensionLayer';
import { legendCategories, severityMarkerRadii, severityRingSeverities } from './mapEvents';

type MapLegendProps = {
  firmsVisible: boolean;
  firmsHasObservations: boolean;
  /** The conflict and tension swatches are listed only while their layers are on. */
  conflictZonesVisible?: boolean;
  tensionVisible?: boolean;
};

const firmsLegendItems = [
  { label: '<3h', className: 'firms-age-under-3h' },
  { label: '3–6h', className: 'firms-age-3-6h' },
  { label: '6–12h', className: 'firms-age-6-12h' },
  { label: '12–24h', className: 'firms-age-12-24h' },
];

/** Severity key glyph diameter: twice the marker radius at world zoom, so the key matches the markers. */
export function severityGlyphDiameter(severity: keyof typeof severityMarkerRadii) {
  return severityMarkerRadii[severity][0] * 2;
}

export function MapLegend({ firmsVisible, firmsHasObservations, conflictZonesVisible = false, tensionVisible = false }: MapLegendProps) {
  return (
    <div className="map-legends">
      <div className="legend" aria-label="semantic legend">
        {legendCategories.map((item) => (
          <span key={item.category} data-category={item.category}>
            <i style={{ background: item.color }} aria-hidden="true" />
            {item.label}
          </span>
        ))}
        {conflictZonesVisible ? (
          <span data-reference-layer="conflict-zones">
            <i className="legend-area-swatch" style={{ background: conflictZoneColors.fill, borderColor: conflictZoneColors.outline }} aria-hidden="true" />
            {conflictLegendLabel}
          </span>
        ) : null}
        {tensionVisible ? (
          <span data-reference-layer="tension">
            <i className="legend-area-swatch" style={{ background: tensionColors.fill, borderColor: tensionColors.fill }} aria-hidden="true" />
            {tensionLegendLabel}
          </span>
        ) : null}
      </div>
      <div className="legend severity-size-legend" aria-label="Marker size shows severity, from Low to Catastrophic; Critical and Catastrophic markers have a light ring">
        <strong>Size = severity</strong>
        <span>Low</span>
        {eventSeverities.map((severity) => (
          <i
            key={severity}
            className={`severity-size severity-size-${severity.toLowerCase()}${severityRingSeverities.includes(severity) ? ' has-ring' : ''}`}
            style={{ width: `${severityGlyphDiameter(severity)}px`, height: `${severityGlyphDiameter(severity)}px` }}
            title={severity}
            aria-hidden="true"
          />
        ))}
        <span>Catastrophic</span>
      </div>
      {firmsVisible && firmsHasObservations ? (
        <div className="legend firms-age-legend" aria-label="FIRMS detection age legend">
          <strong>FIRMS — detection age</strong>
          {firmsLegendItems.map((item) => (
            <span key={item.label}><i className={item.className} aria-hidden="true" />{item.label}</span>
          ))}
          <small>NASA FIRMS / LANCE</small>
        </div>
      ) : null}
    </div>
  );
}
