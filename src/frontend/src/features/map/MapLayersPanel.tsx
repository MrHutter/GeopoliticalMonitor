import { MapLayer } from '../../types/mock';
import { firmsDisplayModeOptions, type FirmsDisplayMode } from './firmsMap';

type MapLayersPanelProps = {
  layers: MapLayer[];
  firmsStatus: FirmsLayerStatus;
  firmsDisplayMode: FirmsDisplayMode;
  firmsShownCount: number;
  firmsTotalCount: number;
  firmsHasSnapshot: boolean;
  onToggleLayer: (layerId: string) => void;
  onRefreshFirms: () => void;
  onFirmsDisplayModeChange: (mode: FirmsDisplayMode) => void;
};

export type FirmsLayerStatus = {
  kind: 'ready' | 'refreshing' | 'success' | 'error' | 'unconfigured' | 'area-too-large';
  message?: string;
};

function statusLabel(status: MapLayer['status']) {
  if (status === 'active') {
    return 'Active';
  }

  if (status === 'demo') {
    return 'Demo';
  }

  return 'Pending';
}

export function MapLayersPanel({
  layers,
  firmsStatus,
  firmsDisplayMode,
  firmsShownCount,
  firmsTotalCount,
  firmsHasSnapshot,
  onToggleLayer,
  onRefreshFirms,
  onFirmsDisplayModeChange,
}: MapLayersPanelProps) {
  const totalLabel = firmsTotalCount === 1 ? 'detection' : 'detections';
  return (
    <div className="map-layer-controls">
      <div className="layer-toggles" aria-label="map layers">
        {layers.map((layer) => (
          <label key={layer.id} className={`${layer.enabled ? 'is-enabled' : ''} layer-status-${layer.status ?? 'pending'}`}>
            <input type="checkbox" checked={layer.enabled} onChange={() => onToggleLayer(layer.id)} />
            <span>{layer.label}</span>
            <em>{statusLabel(layer.status)}</em>
          </label>
        ))}
      </div>
      <div className="firms-refresh-control">
        <label className="firms-display-filter">
          <span>Display filter</span>
          <select
            aria-label="FIRMS display filter"
            value={firmsDisplayMode}
            onChange={(event) => onFirmsDisplayModeChange(event.target.value as FirmsDisplayMode)}
          >
            {firmsDisplayModeOptions.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>
        <button type="button" onClick={onRefreshFirms} disabled={firmsStatus.kind === 'refreshing'}>
          {firmsStatus.kind === 'refreshing' ? 'Refreshing FIRMS...' : 'Refresh FIRMS'}
        </button>
        <span className="firms-window-label">24h</span>
        {firmsHasSnapshot ? (
          <p className="firms-snapshot-summary" aria-label="FIRMS snapshot summary">
            {firmsShownCount.toLocaleString()} shown · {firmsTotalCount.toLocaleString()} {totalLabel} · 24h
          </p>
        ) : null}
        {firmsStatus.message ? (
          <p
            className={`firms-refresh-status is-${firmsStatus.kind}`}
            role={['error', 'unconfigured', 'area-too-large'].includes(firmsStatus.kind) ? 'alert' : 'status'}
          >
            {firmsStatus.message}
          </p>
        ) : null}
      </div>
    </div>
  );
}
