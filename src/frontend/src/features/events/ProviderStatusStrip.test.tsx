import { act, fireEvent, render, screen, within } from '@testing-library/react';
import type { ProviderRefreshStatus } from '../../nativeBridge';
import { formatNextRefresh, formatProviderState, formatUpdated, ProviderStatusStrip } from './ProviderStatusStrip';

const now = Date.parse('2026-09-30T12:00:00Z');

function status(overrides: Partial<ProviderRefreshStatus> = {}): ProviderRefreshStatus {
  return {
    provider: 'Gdacs',
    displayName: 'GDACS',
    state: 'Idle',
    lastSuccessAtUtc: null,
    nextRefreshAtUtc: null,
    message: null,
    newAlerts: 0,
    ...overrides,
  };
}

describe('ProviderStatusStrip', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('formats relative update times, next refresh and state labels', () => {
    expect(formatUpdated(null, now)).toBe('Not updated yet');
    expect(formatUpdated('not-a-time', now)).toBe('Not updated yet');
    expect(formatUpdated(new Date(now - 30_000).toISOString(), now)).toBe('Updated just now');
    expect(formatUpdated(new Date(now - 12 * 60_000).toISOString(), now)).toBe('Updated 12 min ago');
    expect(formatUpdated(new Date(now - 125 * 60_000).toISOString(), now)).toBe('Updated 2 h ago');
    expect(formatNextRefresh(null)).toBe('No refresh scheduled');
    expect(formatNextRefresh('2026-09-30T12:30:00Z')).toMatch(/^Next \d/);
    expect(formatProviderState(status())).toBeNull();
    expect(formatProviderState(status({ state: 'Refreshing' }))).toBe('Refreshing…');
    expect(formatProviderState(status({ state: 'Failed', message: 'Request timed out. Existing events were preserved.' }))).toBe('Request timed out. Existing events were preserved.');
    expect(formatProviderState(status({ state: 'Stale' }))).toBe('Stale');
    expect(formatProviderState(status({ state: 'Unconfigured' }))).toBe('Not configured');
  });

  it('refreshes relative times about every thirty seconds', () => {
    vi.useFakeTimers();
    vi.setSystemTime(now);
    render(<ProviderStatusStrip statuses={[status({ lastSuccessAtUtc: new Date(now - 4 * 60_000 - 45_000).toISOString() })]} gdacsRefreshPending={false} onRefreshGdacs={() => undefined} />);

    expect(screen.getByLabelText('GDACS refresh status')).toHaveTextContent('Updated 4 min ago');

    act(() => {
      vi.advanceTimersByTime(30_000);
    });
    expect(screen.getByLabelText('GDACS refresh status')).toHaveTextContent('Updated 5 min ago');
  });

  it('keeps the GDACS manual action present and disables it while GDACS is refreshing', () => {
    const { rerender } = render(<ProviderStatusStrip statuses={[]} gdacsRefreshPending={false} onRefreshGdacs={() => undefined} />);
    expect(screen.getByRole('button', { name: 'Refresh GDACS' })).toBeEnabled();

    rerender(<ProviderStatusStrip statuses={[status({ state: 'Refreshing' })]} gdacsRefreshPending={false} onRefreshGdacs={() => undefined} />);
    expect(screen.getByRole('button', { name: 'Refresh GDACS' })).toBeDisabled();

    rerender(<ProviderStatusStrip statuses={[status()]} gdacsRefreshPending onRefreshGdacs={() => undefined} />);
    expect(screen.getByRole('button', { name: 'Refreshing...' })).toBeDisabled();
  });

  it('shows each provider in at most two lines: name, state and an inline Refresh button, then a muted detail line', () => {
    vi.useFakeTimers();
    vi.setSystemTime(now);
    const ucdp = status({
      provider: 'UcdpCandidate',
      displayName: 'UCDP Candidate',
      lastSuccessAtUtc: new Date(now - 3 * 60_000).toISOString(),
      nextRefreshAtUtc: '2026-09-30T18:00:00Z',
      message: 'No new release',
      cadenceLabel: 'monthly release',
      dataThrough: '2026-08',
    });
    const onRefreshUcdp = vi.fn();
    render(
      <ProviderStatusStrip
        statuses={[status({ lastSuccessAtUtc: new Date(now - 12 * 60_000).toISOString(), nextRefreshAtUtc: '2026-09-30T12:30:00Z' }), ucdp]}
        gdacsRefreshPending={false}
        onRefreshGdacs={() => undefined}
        onRefreshUcdp={onRefreshUcdp}
      />,
    );

    const gdacs = screen.getByLabelText('GDACS refresh status');
    const [gdacsLine, gdacsDetail] = Array.from(gdacs.querySelectorAll<HTMLElement>(':scope > div'));
    expect(gdacs.querySelectorAll(':scope > div')).toHaveLength(2);
    expect(gdacsLine.lastElementChild).toBe(within(gdacs).getByRole('button', { name: 'Refresh GDACS' }));
    expect(gdacsLine).toHaveTextContent(/^GDACSRefresh$/);
    expect(gdacsDetail).toHaveClass('provider-status-detail');
    expect(gdacsDetail).toHaveTextContent(/^Updated 12 min ago · Next \d{2}:\d{2}$/);
    expect(gdacs).toHaveAttribute('title', expect.stringMatching(/^GDACS · Updated 12 min ago · Next \d{2}:\d{2}$/));

    const row = screen.getByLabelText('UCDP Candidate refresh status');
    const [line, detail] = Array.from(row.querySelectorAll<HTMLElement>(':scope > div'));
    expect(row.querySelectorAll(':scope > div')).toHaveLength(2);
    expect(line).toHaveTextContent(/^UCDP CandidateNo new releaseUpdated 3 min agoRefresh$/);
    expect(line.querySelector('.provider-status-text')).not.toContainElement(within(line).getByRole('button'));
    expect(within(line).getByRole('status')).toHaveTextContent('No new release');
    expect(detail).toHaveTextContent(/^monthly release, data through Aug 2026 · Next check \d{2}:\d{2}$/);
    expect(row).toHaveAttribute('title', expect.stringContaining('monthly release, data through August 2026'));
    fireEvent.click(within(row).getByRole('button', { name: 'Refresh UCDP' }));
    expect(onRefreshUcdp).toHaveBeenCalledTimes(1);
  });

  it('keeps every state: short text on line 1, the fixed failure message on line 2, and the warning treatment', () => {
    const states: Array<[ProviderRefreshStatus['state'], ProviderRefreshStatus['message'], string]> = [
      ['Refreshing', null, 'Refreshing…'],
      ['Stale', null, 'Stale'],
      ['Unconfigured', 'Not configured.', 'Not configured'],
    ];
    for (const [state, message, label] of states) {
      const { unmount } = render(<ProviderStatusStrip statuses={[status({ state, message })]} gdacsRefreshPending={false} onRefreshGdacs={() => undefined} />);
      const row = screen.getByLabelText('GDACS refresh status');
      expect(row).toHaveClass(`is-${state.toLowerCase()}`);
      expect(within(row.querySelector('.provider-status-line') as HTMLElement).getByRole('status')).toHaveTextContent(label);
      expect(within(row).getByRole('button', { name: 'Refresh GDACS' })).toHaveProperty('disabled', state === 'Refreshing');
      unmount();
    }

    render(<ProviderStatusStrip statuses={[status({ state: 'Failed', message: 'Request timed out. Existing events were preserved.' })]} gdacsRefreshPending={false} onRefreshGdacs={() => undefined} />);
    const failed = screen.getByLabelText('GDACS refresh status');
    expect(failed).toHaveClass('is-failed');
    expect(failed.querySelector('.provider-status-line')).toHaveTextContent(/^GDACSFailedRefresh$/);
    const alert = within(failed).getByRole('alert');
    expect(alert).toHaveClass('provider-status-detail', 'provider-status-message');
    expect(alert).toHaveTextContent('Request timed out. Existing events were preserved.');
    expect(within(failed).getByRole('button', { name: 'Refresh GDACS' })).toBeEnabled();
  });
});
