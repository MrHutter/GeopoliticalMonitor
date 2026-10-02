import { createRef } from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
import {
  isEventsListResultMessage,
  isProviderRefreshStatusMessage,
  isUcdpRefreshErrorMessage,
  isUcdpRefreshResultMessage,
  requestUcdpRefresh,
  type ProviderRefreshStatus,
} from '../../nativeBridge';
import { explainSeverity, formatMonthKey, formatReleaseOrdinal, type NativeEvent } from '../../types/events';
import { EventDetailsPanel } from '../event-details/EventDetailsPanel';
import { ProviderStatusStrip } from '../events/ProviderStatusStrip';
import { SettingsPanel } from '../settings/SettingsPanel';
import { ucdpAcknowledgement, ucdpCaveat, ucdpCitation } from './ucdpAttribution';

const ucdpEvent: NativeEvent = {
  id: 'evt-ucdp-cand-900001',
  title: 'State-based violence: Northtown',
  summary: 'Government of Testland versus TLF\nDate: 2026-08-03\nFatalities (best/low/high): 12/10/15\nCode status: Clear\nCandidate data may be revised or removed in later UCDP releases.',
  category: 'MilitaryConflict',
  status: 'Unverified',
  startedAtUtc: '2026-08-03T00:00:00Z',
  lastUpdatedAtUtc: '2026-08-03T23:59:59Z',
  location: { latitude: 10.5, longitude: 20.25, displayName: 'Northtown, Testland', countryCode: null, region: 'Africa' },
  createdAtUtc: '2026-10-02T09:00:00Z',
  updatedAtUtc: '2026-10-02T09:00:00Z',
  sourceCount: 1,
  severity: 'High',
  severityRule: 'ucdp-candidate-v1',
  severityInputs: 'best=12; low=10; high=15',
  provider: 'UcdpCandidate',
  providerLastSeenRelease: 202608,
};

const ucdpStatus: ProviderRefreshStatus = {
  provider: 'UcdpCandidate',
  displayName: 'UCDP Candidate',
  state: 'Idle',
  lastSuccessAtUtc: null,
  nextRefreshAtUtc: null,
  message: null,
  newAlerts: 0,
  cadenceLabel: 'monthly release',
};

const ucdpResult = {
  status: 'Succeeded',
  newestRelease: '2026-08',
  releasesImported: 2,
  newEvents: 3,
  updatedEvents: 1,
  unchangedRecords: 0,
  conflicts: 0,
  skippedMalformedRecords: 0,
  skippedInvalidCoordinates: 0,
  skippedImpreciseLocation: 4,
  skippedZeroFatalities: 5,
  skippedOutsideWindow: 0,
  droppedByCap: 0,
};

describe('UCDP Candidate frontend', () => {
  afterEach(() => {
    delete window.chrome;
  });

  it('accepts the UCDP status row with its optional fields and rejects unknown ones', () => {
    const accepts = (provider: Record<string, unknown>) =>
      isProviderRefreshStatusMessage({ type: 'providers.refresh.status', payload: { providers: [provider] } });

    expect(accepts({ ...ucdpStatus })).toBe(true);
    expect(accepts({ ...ucdpStatus, dataThrough: '2026-08', message: 'No new release' })).toBe(true);
    expect(accepts({ ...ucdpStatus, cadenceLabel: 'weekly release' })).toBe(false);
    expect(accepts({ ...ucdpStatus, dataThrough: '2026-13' })).toBe(false);
    expect(accepts({ ...ucdpStatus, dataThrough: 'August 2026' })).toBe(false);
    expect(accepts({ ...ucdpStatus, releaseUrl: 'https://ucdp.uu.se/downloads/' })).toBe(false);
    expect(accepts({ ...ucdpStatus, message: 'No new release.' })).toBe(false);
  });

  it('sends the fixed empty UCDP refresh request and validates typed responses', () => {
    const postMessage = vi.fn();
    window.chrome = { webview: { postMessage, addEventListener: vi.fn(), removeEventListener: vi.fn() } };

    expect(requestUcdpRefresh()).toBe(true);
    expect(postMessage).toHaveBeenCalledWith({ type: 'providers.ucdp.refresh', payload: {} });

    expect(isUcdpRefreshResultMessage({ type: 'providers.ucdp.refresh.result', payload: ucdpResult })).toBe(true);
    expect(isUcdpRefreshResultMessage({ type: 'providers.ucdp.refresh.result', payload: { ...ucdpResult, status: 'NoNewRelease', newestRelease: null } })).toBe(true);
    expect(isUcdpRefreshResultMessage({ type: 'providers.ucdp.refresh.result', payload: { ...ucdpResult, status: 'TimedOut' } })).toBe(false);
    expect(isUcdpRefreshResultMessage({ type: 'providers.ucdp.refresh.result', payload: { ...ucdpResult, newEvents: -1 } })).toBe(false);
    expect(isUcdpRefreshResultMessage({ type: 'providers.ucdp.refresh.result', payload: { ...ucdpResult, newestRelease: '26.0.8' } })).toBe(false);
    expect(isUcdpRefreshResultMessage({ type: 'providers.ucdp.refresh.result', payload: { ...ucdpResult, url: 'x' } })).toBe(false);
    expect(isUcdpRefreshErrorMessage({ type: 'providers.ucdp.refresh.error', payload: { status: 'TimedOut', message: 'The UCDP request timed out. Existing events were preserved.' } })).toBe(true);
    expect(isUcdpRefreshErrorMessage({ type: 'providers.ucdp.refresh.error', payload: { status: 'TimedOut', message: 'x', stack: 'y' } })).toBe(false);
  });

  it('accepts the optional release ordinal on events and rejects invalid values', () => {
    const list = (event: Record<string, unknown>) =>
      isEventsListResultMessage({ type: 'events.list.result', payload: { events: [event], totalCount: 1, capped: false, requestId: 1 } });

    expect(list({ ...ucdpEvent })).toBe(true);
    const { providerLastSeenRelease: _omitted, ...withoutRelease } = ucdpEvent;
    expect(list(withoutRelease)).toBe(true);
    expect(list({ ...ucdpEvent, providerLastSeenRelease: 202613 })).toBe(false);
    expect(list({ ...ucdpEvent, providerLastSeenRelease: '202608' })).toBe(false);
    expect(list({ ...ucdpEvent, providerLastSeenRelease: null })).toBe(false);
  });

  it('formats release months and explains the UCDP severity rule in words', () => {
    expect(formatReleaseOrdinal(202608)).toBe('2026-08');
    expect(formatMonthKey('2026-08')).toBe('August 2026');
    expect(formatMonthKey('2027-01')).toBe('January 2027');
    expect(explainSeverity(ucdpEvent)).toEqual({ rule: 'UCDP best fatality estimate 12, High', derivedFromProvider: true });
  });

  it('shows the UCDP status row with label, data month and a refresh action disabled while refreshing', () => {
    const onRefreshUcdp = vi.fn();
    const { rerender } = render(
      <ProviderStatusStrip
        statuses={[{ ...ucdpStatus, dataThrough: '2026-08', message: 'No new release', lastSuccessAtUtc: new Date().toISOString() }]}
        gdacsRefreshPending={false}
        onRefreshGdacs={vi.fn()}
        onRefreshUcdp={onRefreshUcdp}
      />,
    );

    const row = screen.getByLabelText('UCDP Candidate refresh status');
    expect(row).toHaveTextContent('monthly release, data through Aug 2026');
    expect(row).toHaveAttribute('title', expect.stringContaining('monthly release, data through August 2026'));
    expect(row).toHaveTextContent('Updated just now');
    expect(row).toHaveTextContent('No new release');
    fireEvent.click(within(row).getByRole('button', { name: 'Refresh UCDP' }));
    expect(onRefreshUcdp).toHaveBeenCalledTimes(1);

    rerender(
      <ProviderStatusStrip statuses={[{ ...ucdpStatus, state: 'Refreshing' }]} gdacsRefreshPending={false} onRefreshGdacs={vi.fn()} onRefreshUcdp={onRefreshUcdp} />,
    );
    expect(within(screen.getByLabelText('UCDP Candidate refresh status')).getByRole('button', { name: 'Refresh UCDP' })).toBeDisabled();
    expect(screen.getByLabelText('UCDP Candidate refresh status')).toHaveTextContent('monthly release, no data yet');

    rerender(
      <ProviderStatusStrip statuses={[ucdpStatus]} gdacsRefreshPending={false} onRefreshGdacs={vi.fn()} ucdpRefreshPending onRefreshUcdp={onRefreshUcdp} />,
    );
    expect(within(screen.getByLabelText('UCDP Candidate refresh status')).getByRole('button', { name: 'Refreshing...' })).toBeDisabled();
  });

  it('shows the UCDP caveat, citation, severity explanation and release in Details', () => {
    render(
      <EventDetailsPanel
        event={ucdpEvent}
        sources={[]}
        activeTab="Overview"
        relatedPosts={[]}
        attachedPostIds={new Set()}
        watchlists={[]}
        watchlistMemberships={null}
        pendingMembershipWatchlistId={null}
        watchlistStatus={null}
        xPostAttachmentFeedback={null}
        reliefWebDiscovery={null}
        onTabChange={vi.fn()}
        onAttachXPost={vi.fn()}
        onSearchReliefWeb={vi.fn()}
        onAttachReliefWeb={vi.fn()}
        onToggleWatchlistMembership={vi.fn()}
        onManageWatchlists={vi.fn()}
      />,
    );

    expect(screen.getByText('UCDP best fatality estimate 12, High')).toBeInTheDocument();
    const provenance = screen.getByLabelText('UCDP Candidate provenance');
    expect(provenance).toHaveTextContent('UCDP Candidate');
    expect(provenance).toHaveTextContent('Unverified');
    expect(provenance).toHaveTextContent(ucdpCaveat);
    expect(provenance).toHaveTextContent('Last seen in release 2026-08');
    expect(provenance).toHaveTextContent(`Citation: ${ucdpCitation}`);
    expect(provenance).toHaveTextContent(ucdpAcknowledgement);
    expect(document.body.textContent?.toLowerCase()).not.toContain('hybrid');
  });

  it('shows a compact read-only Data sources section in Settings', () => {
    render(
      <SettingsPanel accent="orange" appInfo={null} persistenceError={null} triggerRef={createRef()} onAccentChange={vi.fn()} onClose={vi.fn()} />,
    );

    const section = screen.getByRole('heading', { name: 'Data sources' }).closest('section')!;
    expect(section).toHaveTextContent('UCDP Candidate');
    expect(section).toHaveTextContent(ucdpCitation);
    expect(section).toHaveTextContent(ucdpAcknowledgement);
    expect(section).toHaveTextContent('Licensed under CC BY 4.0.');
    expect(within(section).queryByRole('button')).toBeNull();
    expect(within(section).queryByRole('textbox')).toBeNull();
  });
});
