import { eventSeverities, type NativeEvent } from '../../types/events';
import type { Accent } from '../../types/mock';
import {
  eventsToFeatureCollection,
  eventTooltipContent,
  getCategoryColor,
  getEventCoordinates,
  selectedMarkerPaint,
  defaultMarkerStrokeWidth,
  selectedMarkerRadius,
  severityMarkerRadii,
  severityRadiusExpression,
  severityRingColor,
  severityRingSeverities,
  severityRingWidth,
  severityStrokeColorExpression,
  severityStrokeWidthExpression,
  severityZoomStops,
} from './mapEvents';

const nativeEvents: NativeEvent[] = [
  {
    id: 'evt-demo-red-sea-shipping',
    title: 'Commercial shipping disruption reported in Red Sea demo lane',
    summary: 'Demo summary',
    category: 'Infrastructure',
    status: 'Active',
    startedAtUtc: '2026-09-17T11:32:00Z',
    lastUpdatedAtUtc: '2026-09-17T11:38:00Z',
    location: { latitude: 15.8, longitude: 41.9, displayName: 'Southern transit lane', region: 'Red Sea demo corridor' },
    createdAtUtc: '2026-09-17T06:00:00Z',
    updatedAtUtc: '2026-09-17T11:38:00Z',
    sourceCount: 3,
    severity: 'High',
    severityRule: 'manual',
    severityInputs: 'demo data',
    provider: 'Demo',
  },
  {
    id: 'evt-demo-baltic-security',
    title: 'Reported security activity near Baltic demo border area',
    summary: 'Demo summary',
    category: 'Security',
    status: 'Developing',
    startedAtUtc: '2026-09-17T11:48:00Z',
    lastUpdatedAtUtc: '2026-09-17T11:54:00Z',
    location: { latitude: 57.4, longitude: 24.8, displayName: 'Northern corridor', region: 'Baltic demo region' },
    createdAtUtc: '2026-09-17T06:00:00Z',
    updatedAtUtc: '2026-09-17T11:54:00Z',
    sourceCount: 3,
    severity: 'High',
    severityRule: 'manual',
    severityInputs: 'demo data',
    provider: 'Demo',
  },
];

describe('mapEvents', () => {
  it('adapts native event geographic data into a MapLibre GeoJSON source', () => {
    const collection = eventsToFeatureCollection(nativeEvents);
    const redSea = collection.features.find((feature) => feature.properties.id === 'evt-demo-red-sea-shipping');

    expect(collection.features).toHaveLength(nativeEvents.length);
    expect(redSea).toBeDefined();
    expect(redSea?.geometry.coordinates).toEqual([41.9, 15.8]);
    expect(redSea?.properties).toMatchObject({
      title: 'Commercial shipping disruption reported in Red Sea demo lane',
      category: 'Infrastructure',
      sourceCount: 3,
    });
  });

  describe('eventTooltipContent', () => {
    const renderTooltip = (properties: Record<string, unknown> | null | undefined) => {
      const container = document.createElement('div');
      container.append(eventTooltipContent(properties));
      return container;
    };

    it('builds the same title, place and source count markup as direct children', () => {
      const { properties } = eventsToFeatureCollection(nativeEvents).features[0];

      expect(renderTooltip(properties).innerHTML).toBe(
        '<strong>Commercial shipping disruption reported in Red Sea demo lane</strong>'
        + '<span>Red Sea demo corridor / Southern transit lane</span>'
        + '<span>3 sources</span>',
      );
    });

    it('falls back to the fixed text when properties are missing', () => {
      for (const properties of [{}, null, undefined]) {
        expect(renderTooltip(properties).innerHTML).toBe('<strong>Local event</strong><span> / </span><span>0 sources</span>');
      }
    });

    it.each(['<img src=x onerror=alert(1)>', '<b>bold</b>', '&amp;'])('renders %s in the title and place as literal text and creates no element from it', (payload) => {
      const hostile: NativeEvent = {
        ...nativeEvents[0],
        title: payload,
        location: { ...nativeEvents[0].location, displayName: payload, region: payload },
      };
      const tooltip = renderTooltip(eventsToFeatureCollection([hostile]).features[0].properties);

      expect([...tooltip.querySelectorAll('*')].map((element) => element.tagName)).toEqual(['STRONG', 'SPAN', 'SPAN']);
      expect(tooltip.querySelector('strong')?.textContent).toBe(payload);
      expect(tooltip.querySelectorAll('span')[0].textContent).toBe(`${payload} / ${payload}`);
    });
  });

  it('resolves event coordinates as valid longitude and latitude pairs', () => {
    expect(getEventCoordinates(nativeEvents[0])).toEqual([41.9, 15.8]);

    eventsToFeatureCollection(nativeEvents).features.forEach((feature) => {
      const [longitude, latitude] = feature.geometry.coordinates;
      expect(longitude).toBeGreaterThanOrEqual(-180);
      expect(longitude).toBeLessThanOrEqual(180);
      expect(latitude).toBeGreaterThanOrEqual(-90);
      expect(latitude).toBeLessThanOrEqual(90);
    });
  });

  it('changes selected-marker accent treatment without changing semantic colors', () => {
    const expectedSelectionColors: Record<Accent, string> = {
      orange: '#ff8a1f',
      blue: '#22d3ff',
      red: '#ff4d5e',
      green: '#3dff87',
      yellow: '#ffe45c',
    };

    Object.entries(expectedSelectionColors).forEach(([accent, color]) => {
      expect(selectedMarkerPaint(accent as Accent)['circle-stroke-color']).toBe(color);
      expect(selectedMarkerPaint(accent as Accent)['circle-color']).toBe(color);
    });

    expect(getCategoryColor('MilitaryConflict')).toBe('#d95d62');
    expect(getCategoryColor('Security')).toBe('#6da3d9');
    expect(getCategoryColor('NaturalDisaster')).toBe('#79a86b');
  });

  it('carries severity into map features', () => {
    const collection = eventsToFeatureCollection(nativeEvents);

    expect(collection.features.map((feature) => feature.properties.severity)).toEqual(['High', 'High']);
  });

  it('grows marker radius with severity in five steps and keeps the selected ring larger', () => {
    severityZoomStops.forEach((_, stopIndex) => {
      const radii = eventSeverities.map((severity) => severityMarkerRadii[severity][stopIndex]);
      radii.slice(1).forEach((radius, index) => expect(radius).toBeGreaterThan(radii[index]));
      expect(radii[radii.length - 1] + severityRingWidth).toBeLessThan(selectedMarkerRadius);
    });

    expect(severityRadiusExpression.slice(0, 3)).toEqual(['interpolate', ['linear'], ['zoom']]);
    for (const severity of eventSeverities) {
      expect(JSON.stringify(severityRadiusExpression)).toContain(`"${severity}"`);
    }
  });

  it('adds a light ring only for Critical and Catastrophic, distinct from the accent selection ring', () => {
    expect(severityRingSeverities).toEqual(['Critical', 'Catastrophic']);
    expect(severityStrokeColorExpression).toEqual(['match', ['get', 'severity'], ['Critical', 'Catastrophic'], severityRingColor, '#0b1016']);
    expect(severityStrokeWidthExpression).toEqual(['match', ['get', 'severity'], ['Critical', 'Catastrophic'], severityRingWidth, defaultMarkerStrokeWidth]);
    expect(defaultMarkerStrokeWidth).toBe(1);
    expect(severityRingWidth).toBe(1.5);

    for (const accent of ['orange', 'blue', 'red', 'green', 'yellow'] as Accent[]) {
      expect(selectedMarkerPaint(accent)['circle-stroke-color']).not.toBe(severityRingColor);
    }
  });

});
