import { fireEvent, render, screen, within } from '@testing-library/react';
import { getVisibleLayerToggles, initialMapLayers, placeholderMapLayerIds, showPlaceholderMapLayers } from '../../mock/dashboardData';
import { eventSeverities, formatCategory, type NativeEventCategory } from '../../types/events';
import { MapLayersPanel } from './MapLayersPanel';
import { MapLegend, severityGlyphDiameter } from './MapLegend';
import {
  categoryColors,
  legendCategories,
  selectedMarkerRadius,
  severityMarkerRadii,
  severityRingWidth,
  severityZoomStops,
} from './mapEvents';

const allCategories: NativeEventCategory[] = [
  'MilitaryConflict', 'Security', 'PoliticalDiplomatic', 'CivilUnrest', 'Infrastructure', 'Humanitarian', 'NaturalDisaster', 'Economic', 'Other',
];

function renderLayers(showPlaceholderLayers?: boolean, onToggleLayer = vi.fn()) {
  return render(
    <MapLayersPanel
      layers={initialMapLayers}
      firmsStatus={{ kind: 'ready' }}
      firmsDisplayMode="significant"
      firmsShownCount={0}
      firmsTotalCount={0}
      firmsHasSnapshot={false}
      onToggleLayer={onToggleLayer}
      onRefreshFirms={vi.fn()}
      onFirmsDisplayModeChange={vi.fn()}
      {...(showPlaceholderLayers === undefined ? {} : { showPlaceholderLayers })}
    />,
  );
}

const toggleLabels = () => within(screen.getByLabelText('map layers')).getAllByRole('checkbox').map((box) => box.closest('label')?.querySelector('span')?.textContent);

describe('map layer toggles', () => {
  it('hides the placeholder Aircraft, Naval and Internet outages toggles by default', () => {
    expect(showPlaceholderMapLayers).toBe(false);
    expect(placeholderMapLayerIds).toEqual(['aircraft', 'naval', 'internet-outages']);
    renderLayers();

    expect(toggleLabels()).toEqual(['Events', 'Conflict zones', 'Tension', 'NASA FIRMS — Fire / Thermal Anomalies', 'Infrastructure']);
  });

  it('lists every toggle again when the flag is on, and they still toggle', () => {
    const onToggleLayer = vi.fn();
    renderLayers(true, onToggleLayer);

    expect(toggleLabels()).toEqual(['Events', 'Conflict zones', 'Tension', 'NASA FIRMS — Fire / Thermal Anomalies', 'Aircraft', 'Naval', 'Infrastructure', 'Internet outages']);
    fireEvent.click(screen.getByLabelText(/Naval/));
    expect(onToggleLayer).toHaveBeenCalledWith('naval');
  });

  it('keeps the kept layers and their state unchanged and never alters the layer model', () => {
    const visible = getVisibleLayerToggles(initialMapLayers, false);

    expect(visible.map((layer) => [layer.id, layer.enabled, layer.status])).toEqual([
      ['events', true, 'active'],
      ['conflict-zones', true, 'active'],
      ['tension', true, 'active'],
      ['firms', false, 'active'],
      ['infrastructure', false, 'pending'],
    ]);
    expect(getVisibleLayerToggles(initialMapLayers, true)).toBe(initialMapLayers);
    expect(initialMapLayers.find((layer) => layer.id === 'naval')?.enabled).toBe(true);
  });
});

describe('conflict and tension toggles', () => {
  it('lists Conflict zones and Tension as active, default-on layers without a DEMO badge', () => {
    renderLayers();
    const conflict = screen.getByLabelText(/Conflict zones/).closest('label');
    const tension = screen.getByLabelText(/^Tension/).closest('label');

    expect(screen.getByLabelText(/Conflict zones/)).toBeChecked();
    expect(screen.getByLabelText(/^Tension/)).toBeChecked();
    expect(conflict?.querySelector('em')).toHaveTextContent('Active');
    expect(tension?.querySelector('em')).toHaveTextContent('Active');
    expect(screen.getByLabelText('map layers')).not.toHaveTextContent('Demo');
  });
});

describe('map legend', () => {
  it('adds the conflict and tension swatches only while their layers are on', () => {
    const { rerender } = render(<MapLegend firmsVisible={false} firmsHasObservations={false} conflictZonesVisible tensionVisible />);
    const legend = screen.getByLabelText('semantic legend');

    expect(legend.querySelector('[data-reference-layer="conflict-zones"]')).toHaveTextContent('Armed conflict (approx.)');
    expect(legend.querySelector('[data-reference-layer="tension"]')).toHaveTextContent('Elevated tension');
    rerender(<MapLegend firmsVisible={false} firmsHasObservations={false} conflictZonesVisible={false} tensionVisible />);
    expect(legend.querySelector('[data-reference-layer="conflict-zones"]')).toBeNull();
    rerender(<MapLegend firmsVisible={false} firmsHasObservations={false} />);
    expect(legend.querySelector('[data-reference-layer]')).toBeNull();
  });

  it('has exactly one entry per Event category, colored from the marker palette and labelled like the list', () => {
    render(<MapLegend firmsVisible={false} firmsHasObservations={false} />);
    const legend = screen.getByLabelText('semantic legend');
    const entries = Array.from(legend.querySelectorAll<HTMLElement>('[data-category]'));

    expect(entries.map((entry) => entry.dataset.category).sort()).toEqual([...allCategories].sort());
    expect(legendCategories.map((item) => item.label)).toEqual([
      'Military conflict', 'Security', 'Political / diplomatic', 'Civil unrest', 'Infrastructure', 'Humanitarian', 'Economic', 'Natural Disaster', 'Other',
    ]);
    for (const entry of entries) {
      const category = entry.dataset.category as NativeEventCategory;
      expect(entry).toHaveTextContent(formatCategory(category));
      expect(entry.querySelector('i')?.getAttribute('style')).toContain(`background: ${hexToRgb(categoryColors[category])}`);
    }
    expect(legend).not.toHaveTextContent('Maritime');
  });

  it('draws the severity key glyphs at the marker sizes, ringed for Critical and Catastrophic', () => {
    render(<MapLegend firmsVisible={false} firmsHasObservations={false} />);
    const glyphs = Array.from(document.querySelectorAll<HTMLElement>('.severity-size'));

    expect(glyphs).toHaveLength(eventSeverities.length);
    glyphs.forEach((glyph, index) => {
      const severity = eventSeverities[index];
      expect(glyph.style.width).toBe(`${severityMarkerRadii[severity][0] * 2}px`);
      expect(glyph.style.height).toBe(`${severityGlyphDiameter(severity)}px`);
      expect(glyph.classList.contains('has-ring')).toBe(severity === 'Critical' || severity === 'Catastrophic');
    });
  });
});

describe('marker radii', () => {
  it('are strictly increasing, at least 3px, about 28% smaller, with the selected ring clearly larger', () => {
    const original = { Low: [3.5, 5, 6], Medium: [4.5, 6.5, 8], High: [5.5, 7.5, 9], Critical: [6.5, 8.5, 10], Catastrophic: [7.5, 9.5, 11] } as const;
    severityZoomStops.forEach((_, stopIndex) => {
      const radii = eventSeverities.map((severity) => severityMarkerRadii[severity][stopIndex]);
      radii.forEach((radius) => expect(radius).toBeGreaterThanOrEqual(3));
      radii.slice(1).forEach((radius, index) => expect(radius).toBeGreaterThan(radii[index]));
      expect(radii[radii.length - 1] + severityRingWidth).toBeLessThan(selectedMarkerRadius - 1);
    });
    // At world zoom Low and Medium are held up by the 3 px floor and the strictly increasing rule.
    for (const severity of eventSeverities) {
      original[severity].forEach((value, stopIndex) => {
        if (stopIndex === 0 && (severity === 'Low' || severity === 'Medium')) return;
        const reduction = 1 - severityMarkerRadii[severity][stopIndex] / value;
        expect(reduction).toBeGreaterThan(0.2);
        expect(reduction).toBeLessThan(0.33);
      });
    }
  });
});

function hexToRgb(hex: string) {
  const value = Number.parseInt(hex.slice(1), 16);
  return `rgb(${(value >> 16) & 255}, ${(value >> 8) & 255}, ${value & 255})`;
}
