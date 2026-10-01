import { act, render, screen } from '@testing-library/react';
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
});
