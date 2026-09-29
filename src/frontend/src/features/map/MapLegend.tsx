const legendItems = [
  { label: 'Conflict', className: 'legend-conflict' },
  { label: 'Security', className: 'legend-security' },
  { label: 'Maritime', className: 'legend-maritime' },
  { label: 'Civil', className: 'legend-civil' },
  { label: 'Infrastructure', className: 'legend-infrastructure' },
  { label: 'Humanitarian', className: 'legend-humanitarian' },
  { label: 'Natural disaster', className: 'legend-natural-disaster' },
];

type MapLegendProps = { firmsVisible: boolean; firmsHasObservations: boolean };

const firmsLegendItems = [
  { label: '<3h', className: 'firms-age-under-3h' },
  { label: '3–6h', className: 'firms-age-3-6h' },
  { label: '6–12h', className: 'firms-age-6-12h' },
  { label: '12–24h', className: 'firms-age-12-24h' },
];

export function MapLegend({ firmsVisible, firmsHasObservations }: MapLegendProps) {
  return (
    <div className="map-legends">
      <div className="legend" aria-label="semantic legend">
        {legendItems.map((item) => (
          <span key={item.label}>
            <i className={item.className} aria-hidden="true" />
            {item.label}
          </span>
        ))}
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
