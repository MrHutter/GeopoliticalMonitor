import { Accent, MapLayer } from '../../types/mock';
import { NativeEvent } from '../../types/events';
import type { FirmsObservation, FirmsViewport } from '../../nativeBridge';
import { MapLayersPanel } from './MapLayersPanel';
import type { FirmsLayerStatus } from './MapLayersPanel';
import { MapLegend } from './MapLegend';
import { MapLibreWorldMap } from './MapLibreWorldMap';
import type { FirmsDisplayMode } from './firmsMap';

type LocateRequest = {
  eventId: string;
  requestId: number;
};

type MapWorkspaceProps = {
  accent: Accent;
  events: NativeEvent[];
  layers: MapLayer[];
  selectedEventId: string;
  locateRequest: LocateRequest | null;
  firmsObservations: FirmsObservation[];
  firmsRefreshedAtUtc: string | null;
  firmsStatus: FirmsLayerStatus;
  firmsDisplayMode: FirmsDisplayMode;
  firmsTotalCount: number;
  onSelectEvent: (eventId: string) => void;
  onToggleLayer: (layerId: string) => void;
  onRefreshFirms: () => void;
  onViewportChange: (viewport: FirmsViewport) => void;
  onFirmsDisplayModeChange: (mode: FirmsDisplayMode) => void;
};

export function MapWorkspace({
  accent,
  events,
  layers,
  selectedEventId,
  locateRequest,
  firmsObservations,
  firmsRefreshedAtUtc,
  firmsStatus,
  firmsDisplayMode,
  firmsTotalCount,
  onSelectEvent,
  onToggleLayer,
  onRefreshFirms,
  onViewportChange,
  onFirmsDisplayModeChange,
}: MapWorkspaceProps) {
  const firmsVisible = layers.some((layer) => layer.id === 'firms' && layer.enabled);
  return (
    <section className="map-workspace panel" aria-labelledby="map-title">
      <div className="map-toolbar">
        <div className="map-title">
          <p className="eyebrow">Map</p>
          <h2 id="map-title">Local MapLibre world map</h2>
        </div>
        <MapLayersPanel
          layers={layers}
          firmsStatus={firmsStatus}
          firmsDisplayMode={firmsDisplayMode}
          firmsShownCount={firmsObservations.length}
          firmsTotalCount={firmsTotalCount}
          firmsHasSnapshot={firmsRefreshedAtUtc !== null}
          onToggleLayer={onToggleLayer}
          onRefreshFirms={onRefreshFirms}
          onFirmsDisplayModeChange={onFirmsDisplayModeChange}
        />
      </div>

      <MapLibreWorldMap
        accent={accent}
        events={events}
        layers={layers}
        selectedEventId={selectedEventId}
        locateRequest={locateRequest}
        firmsObservations={firmsObservations}
        firmsRefreshedAtUtc={firmsRefreshedAtUtc}
        onSelectEvent={onSelectEvent}
        onViewportChange={onViewportChange}
      />

      <div className="map-footer">
        <MapLegend firmsVisible={firmsVisible} firmsHasObservations={firmsObservations.length > 0} />
        <span className="demo-note">Bundled Natural Earth 110m basemap. No remote tiles or map server.</span>
      </div>
    </section>
  );
}
