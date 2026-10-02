import { createRef } from 'react';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { render, screen, within } from '@testing-library/react';
import { TopBar } from './features/settings/TopBar';
import { EventPanel } from './features/events/EventPanel';
import { MapLayersPanel } from './features/map/MapLayersPanel';
import { initialMapLayers } from './mock/dashboardData';

// jsdom computes no layout and does not evaluate container queries; these tests check the DOM and the stylesheet rules
// that produce the responsive layout.

const css = readFileSync(join(process.cwd(), 'src', 'styles.css'), 'utf8');

function block(selectorStart: string) {
  const start = css.indexOf(selectorStart);
  expect(start).toBeGreaterThanOrEqual(0);
  let depth = 0;
  for (let index = css.indexOf('{', start); index < css.length; index++) {
    if (css[index] === '{') depth++;
    if (css[index] === '}' && --depth === 0) return css.slice(start, index + 1);
  }
  throw new Error(`Unclosed block ${selectorStart}`);
}

/** The top-level (unindented) rule for a selector, skipping copies inside container or media queries. */
function topLevelBlock(selector: string) {
  return block(`\n${selector} {`);
}

function renderTopBar(unreadAlertCount: number) {
  return render(
    <TopBar
      searchQuery=""
      onSearchQueryChange={vi.fn()}
      selectedRange="ALL"
      onRangeChange={vi.fn()}
      unreadAlertCount={unreadAlertCount}
      alertsButtonRef={createRef()}
      onOpenAlerts={vi.fn()}
      settingsButtonRef={createRef()}
      onOpenSettings={vi.fn()}
    />,
  );
}

describe('responsive layout', () => {
  it('always gives the Alerts button an accessible name and a visible label, with or without unread alerts', () => {
    const { unmount } = renderTopBar(0);
    const alerts = screen.getByRole('button', { name: 'Alerts' });
    expect(alerts.querySelector('span')).toHaveTextContent('Alerts');
    unmount();

    renderTopBar(3);
    const unread = screen.getByRole('button', { name: 'Alerts, 3 unread' });
    expect(unread.querySelector('span')).toHaveTextContent('Alerts');
    expect(unread.querySelector('.alerts-badge')).toHaveTextContent('3');
  });

  it('keeps the top bar on one row at narrow widths and no longer hides the Alerts label', () => {
    const narrow = block('@media (max-width: 1120px)');

    expect(narrow).toContain('grid-template-columns: auto minmax(180px, 1fr) auto;');
    expect(narrow).not.toContain('.alerts-entry > span');
    expect(narrow).not.toContain('grid-row: 2');
    expect(css).not.toMatch(/\.alerts-entry > span\s*\{[^}]*clip:/);
    expect(block('.search-box input {')).toContain('min-width: 180px;');
    expect(block('.top-actions {')).toContain('flex-wrap: nowrap;');
  });

  it('makes the Events panel a size container with narrow defaults and container-query relaxations', () => {
    expect(block('.event-panel {')).toContain('container: events-panel / inline-size;');
    expect(css).toContain('@container events-panel (max-width: 259px)');
    // The former 340px relaxation could never match: the widest Events container is 338px (340px column, 1px borders).
    expect(css).not.toContain('@container events-panel (min-width: 340px)');
    expect(block('.provider-status-line {')).toContain('flex-wrap: nowrap;');
    expect(block('.provider-status-line .gdacs-refresh-button {')).toContain('flex: 0 0 auto;');
    expect(block('.provider-status-text {')).toContain('text-overflow: ellipsis;');
    expect(block('.workspace-grid {')).toContain('--events-panel-width: clamp(300px, 28%, 320px);');
    expect(css).toContain('--events-panel-width: clamp(300px, 20%, 340px);');
    expect(css).toContain('--events-panel-width: clamp(300px, 28%, 310px);');
  });

  it('keeps the counter and the capped notice on one line without a wrap-and-clip pattern', () => {
    for (const selector of ['.summary-strip {', '.event-window-notice {']) {
      const rule = block(selector);
      expect(rule).toContain('display: block;');
      expect(rule).toContain('white-space: nowrap;');
      expect(rule).toContain('text-overflow: ellipsis;');
      expect(rule).toContain('line-height: 14px;');
      // The replaced rules: wrapping flex lines inside a fixed clipped height leaked a sliver of the next line.
      expect(rule).not.toContain('flex-wrap: wrap;');
      expect(rule).not.toMatch(/height: (1\.3em|calc)/);
      expect(rule).not.toMatch(/margin[^:]*: -/);
    }
    expect(topLevelBlock('.provider-status-detail')).toContain('line-height: 13px;');
    expect(topLevelBlock('.provider-status-detail')).toContain('text-overflow: ellipsis;');
    expect(block('.provider-status-text {')).toContain('line-height: 14px;');
  });

  it('hides the optional header text visually below fixed container thresholds', () => {
    const hidden = block('.summary-strip .summary-sources,');
    expect(hidden).toContain('.event-window-notice .event-window-notice-advice {');
    expect(hidden).toContain('position: absolute;');
    expect(hidden).toContain('clip: rect(0, 0, 0, 0);');
    expect(hidden).not.toMatch(/margin[^:]*: -/);

    const sources = block('@container events-panel (min-width: 350px)');
    expect(sources).toContain('.summary-strip .summary-sources {');
    expect(sources).toContain('position: static;');
    const advice = block('@container events-panel (min-width: 370px)');
    expect(advice).toContain('.event-window-notice .event-window-notice-advice {');
    expect(advice).toContain('position: static;');
  });

  it('lets the layer toggles fill each toolbar row and keeps the FIRMS controls on a row of their own', () => {
    expect(block('.map-toolbar {')).toContain('justify-content: space-between;');
    expect(block('.map-toolbar {')).toContain('flex-wrap: wrap;');
    expect(block('.map-layer-controls,')).toContain('display: contents;');
    expect(block('.map-title {')).toContain('flex: 0 0 auto;');
    const toggle = block('.layer-toggles label {');
    expect(toggle).toContain('flex: 1 1 auto;');
    expect(toggle).toContain('max-width: 360px;');
    expect(toggle).toContain('white-space: nowrap;');
    expect(block('.layer-toggles label em {')).toContain('margin-left: auto;');
    const firms = block('.firms-refresh-control {');
    expect(firms).toContain('flex: 1 0 100%;');
    expect(firms).toContain('justify-content: flex-start;');
    const firmsText = block('.firms-refresh-control .firms-snapshot-summary,');
    expect(firmsText).toContain('flex: 1 1 auto;');
    expect(firmsText).toContain('text-align: right;');
  });

  it('still renders every toolbar control, with the FIRMS controls, summary and status grouped together', () => {
    const { container } = render(
      <MapLayersPanel
        layers={initialMapLayers}
        firmsStatus={{ kind: 'success', message: 'FIRMS refreshed.' }}
        firmsDisplayMode="significant"
        firmsShownCount={1234}
        firmsTotalCount={5678}
        firmsHasSnapshot
        onToggleLayer={vi.fn()}
        onRefreshFirms={vi.fn()}
        onFirmsDisplayModeChange={vi.fn()}
      />,
    );

    const toggles = screen.getByLabelText('map layers').querySelectorAll('label');
    expect(Array.from(toggles, (label) => label.querySelector('span')?.textContent)).toEqual(['Events', 'Conflict zones', 'Tension', 'NASA FIRMS — Fire / Thermal Anomalies', 'Infrastructure']);
    const firms = container.querySelector('.firms-refresh-control');
    expect(firms).not.toBeNull();
    expect(Array.from(firms!.children, (child) => child.className)).toEqual(['firms-display-filter', '', 'firms-window-label', 'firms-snapshot-summary', 'firms-refresh-status is-success']);
    expect(within(firms as HTMLElement).getByRole('combobox', { name: 'FIRMS display filter' })).toBeInTheDocument();
    expect(within(firms as HTMLElement).getByRole('button', { name: 'Refresh FIRMS' })).toBeInTheDocument();
    expect(within(firms as HTMLElement).getByText('24h')).toBeInTheDocument();
    expect(within(firms as HTMLElement).getByLabelText('FIRMS snapshot summary')).toHaveTextContent('1,234 shown · 5,678 detections · 24h');
    expect(within(firms as HTMLElement).getByRole('status')).toHaveTextContent('FIRMS refreshed.');
  });

  it('puts the full selected labels in the select titles', () => {
    render(
      <EventPanel
        events={[]}
        selectedEventId={null}
        selectedRange="ALL"
        loadError={null}
        watchlists={[{ id: 'wl-00000000000000000000000000000001', name: 'A very long watchlist name for the region', eventCount: 0 }]}
        activeWatchlistId="wl-00000000000000000000000000000001"
        minimumSeverity="Critical"
        eventWindow={{ totalCount: 0, capped: false, settled: true }}
        emptyMessage="No events."
        manageButtonRef={createRef()}
        gdacsRefresh={{ kind: 'idle' }}
        providerStatuses={[]}
        onRefreshGdacs={vi.fn()}
        onSelectEvent={vi.fn()}
        onSelectWatchlist={vi.fn()}
        onMinimumSeverityChange={vi.fn()}
        onManageWatchlists={vi.fn()}
      />,
    );

    expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveAttribute('title', 'A very long watchlist name for the region');
    expect(screen.getByRole('combobox', { name: 'Minimum severity' })).toHaveAttribute('title', 'Critical and above');
  });
});
