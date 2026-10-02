import { MapLayer, MockXPost } from '../types/mock';

export const mockXPosts: MockXPost[] = [
  {
    id: 'x-baltic-1',
    displayName: 'Demo Border Monitor',
    handle: '@demo_border_watch',
    timestamp: '11m',
    text: 'Demo-only note: multiple local references mention unusual security activity near a northern border corridor.',
    relatedEventId: 'evt-demo-baltic-security',
  },
  {
    id: 'x-redsea-1',
    displayName: 'Maritime Demo Desk',
    handle: '@demo_maritime',
    timestamp: '24m',
    text: 'Mock shipping desk update: operators are discussing route adjustments in a Red Sea transit lane.',
    relatedEventId: 'evt-demo-red-sea-shipping',
  },
  {
    id: 'x-diplomatic-1',
    displayName: 'Diplomatic Wire Demo',
    handle: '@demo_diplo_wire',
    timestamp: '42m',
    text: 'Fictional briefing: regional consultation expected after a round of talks. Demonstration content only.',
    relatedEventId: 'evt-demo-europe-diplomatic',
  },
  {
    id: 'x-infra-1',
    displayName: 'Infrastructure Watch Mock',
    handle: '@demo_infra_watch',
    timestamp: '1h',
    text: 'Placeholder logistics observation for UI testing. No live transport or port data is connected.',
    relatedEventId: 'evt-demo-east-asia-infrastructure',
  },
  {
    id: 'x-civic-1',
    displayName: 'Civic Signal Demo',
    handle: '@demo_civic',
    timestamp: '2h',
    text: 'Demo civic-observer post for the Following panel. Used to test Locate, Attach, and Save actions.',
    relatedEventId: 'evt-demo-north-africa-civil',
  },
  {
    id: 'x-open-source-1',
    displayName: 'Open Source Demo Notes',
    handle: '@demo_osint_notes',
    timestamp: '3h',
    text: 'Reminder: SourceItem does not equal Event. This mock post is source material, not authority.',
  },
];

/** Toggles for layers that have no data source yet. They never affect the map; the flag only controls whether they are listed. */
export const placeholderMapLayerIds: readonly string[] = ['aircraft', 'naval', 'internet-outages'];
export const showPlaceholderMapLayers = false;

export function getVisibleLayerToggles(layers: MapLayer[], showPlaceholders: boolean = showPlaceholderMapLayers) {
  return showPlaceholders ? layers : layers.filter((layer) => !placeholderMapLayerIds.includes(layer.id));
}

export const initialMapLayers: MapLayer[] = [
  { id: 'events', label: 'Events', enabled: true, status: 'active' },
  { id: 'conflict-zones', label: 'Conflict zones', enabled: true, status: 'demo' },
  { id: 'firms', label: 'NASA FIRMS — Fire / Thermal Anomalies', enabled: false, status: 'active' },
  { id: 'aircraft', label: 'Aircraft', enabled: false, status: 'pending' },
  { id: 'naval', label: 'Naval', enabled: true, status: 'pending' },
  { id: 'infrastructure', label: 'Infrastructure', enabled: false, status: 'pending' },
  { id: 'internet-outages', label: 'Internet outages', enabled: false, status: 'pending' },
];
