import { useEffect, useState, type FormEvent } from 'react';
import type { NativeEvent, ReliefWebReportCandidate } from '../../types/events';
import { formatEventTime } from '../../types/events';

export type ReliefWebDiscoveryState = {
  eventId: string;
  kind: 'idle' | 'searching' | 'results' | 'error';
  candidates: ReliefWebReportCandidate[];
  message?: string;
  attachingReportId?: string;
  attachedReportIds: ReadonlySet<string>;
};

type ReliefWebDiscoveryProps = {
  event: NativeEvent;
  state: ReliefWebDiscoveryState;
  onSearch: (eventId: string, query: string, fromDateUtc: string, toDateUtc: string, countryIso3?: string) => void;
  onAttach: (eventId: string, reportId: string) => void;
};

function dateInputValue(value: string, dayOffset: number) {
  const date = new Date(value);
  date.setUTCDate(date.getUTCDate() + dayOffset);
  return date.toISOString().slice(0, 10);
}

export function ReliefWebDiscovery({ event, state, onSearch, onAttach }: ReliefWebDiscoveryProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [query, setQuery] = useState(event.title);
  const [fromDate, setFromDate] = useState(dateInputValue(event.startedAtUtc, -3));
  const [toDate, setToDate] = useState(dateInputValue(event.lastUpdatedAtUtc, 14));
  const [countryIso3, setCountryIso3] = useState(
    event.location.countryCode?.length === 3 ? event.location.countryCode.toUpperCase() : '',
  );
  const [validationMessage, setValidationMessage] = useState<string | null>(null);

  useEffect(() => {
    setIsOpen(false);
    setQuery(event.title);
    setFromDate(dateInputValue(event.startedAtUtc, -3));
    setToDate(dateInputValue(event.lastUpdatedAtUtc, 14));
    setCountryIso3(event.location.countryCode?.length === 3 ? event.location.countryCode.toUpperCase() : '');
    setValidationMessage(null);
  }, [event.id, event.lastUpdatedAtUtc, event.location.countryCode, event.startedAtUtc, event.title]);

  const submit = (formEvent: FormEvent<HTMLFormElement>) => {
    formEvent.preventDefault();
    const normalizedQuery = query.trim();
    const normalizedCountry = countryIso3.trim().toUpperCase();
    if (!normalizedQuery || normalizedQuery.length > 200) {
      setValidationMessage('Enter a query of 200 characters or fewer.');
      return;
    }
    if (!fromDate || !toDate || fromDate > toDate) {
      setValidationMessage('Enter an ordered date range.');
      return;
    }
    if (normalizedCountry && !/^[A-Z]{3}$/.test(normalizedCountry)) {
      setValidationMessage('Country must be a three-letter ISO code.');
      return;
    }

    const fromDateUtc = new Date(`${fromDate}T00:00:00.000Z`).toISOString();
    const toDateUtc = new Date(`${toDate}T23:59:59.999Z`).toISOString();
    const maximumWindowMs = 366 * 24 * 60 * 60 * 1000;
    if (new Date(toDateUtc).getTime() - new Date(fromDateUtc).getTime() > maximumWindowMs) {
      setValidationMessage('Date range must be 366 days or fewer.');
      return;
    }

    setValidationMessage(null);
    onSearch(event.id, normalizedQuery, fromDateUtc, toDateUtc, normalizedCountry || undefined);
  };

  return (
    <section className="reliefweb-discovery" aria-label="ReliefWeb report discovery">
      <div className="reliefweb-discovery-heading">
        <div>
          <strong>ReliefWeb reports</strong>
          <span>Metadata via ReliefWeb</span>
        </div>
        <button type="button" onClick={() => setIsOpen((current) => !current)}>
          {isOpen ? 'Close' : 'Find ReliefWeb reports'}
        </button>
      </div>

      {isOpen ? (
        <form className="reliefweb-search-form" onSubmit={submit}>
          <label className="reliefweb-query-field">
            <span>Query</span>
            <input value={query} onChange={(inputEvent) => setQuery(inputEvent.target.value)} maxLength={200} disabled={state.kind === 'searching'} />
          </label>
          <label>
            <span>From</span>
            <input type="date" value={fromDate} onChange={(inputEvent) => setFromDate(inputEvent.target.value)} disabled={state.kind === 'searching'} />
          </label>
          <label>
            <span>To</span>
            <input type="date" value={toDate} onChange={(inputEvent) => setToDate(inputEvent.target.value)} disabled={state.kind === 'searching'} />
          </label>
          <label>
            <span>Country ISO3</span>
            <input value={countryIso3} onChange={(inputEvent) => setCountryIso3(inputEvent.target.value.toUpperCase())} maxLength={3} placeholder="Optional" disabled={state.kind === 'searching'} />
          </label>
          <div className="reliefweb-search-context">
            <span>{event.category}</span>
            <button type="submit" disabled={state.kind === 'searching'}>{state.kind === 'searching' ? 'Searching...' : 'Search reports'}</button>
          </div>
        </form>
      ) : null}

      {validationMessage ? <p className="attachment-feedback is-error" role="alert">{validationMessage}</p> : null}
      {!validationMessage && state.message ? <p className={`attachment-feedback is-${state.kind === 'error' ? 'error' : 'info'}`} role={state.kind === 'error' ? 'alert' : 'status'}>{state.message}</p> : null}

      {state.candidates.length > 0 ? (
        <div className="reliefweb-results" aria-label="ReliefWeb report candidates">
          {state.candidates.map((candidate) => {
            const isAttached = state.attachedReportIds.has(candidate.reportId);
            const isAttaching = state.attachingReportId === candidate.reportId;
            return (
              <article key={candidate.reportId}>
                <div className="reliefweb-result-copy">
                  <strong>{candidate.title}</strong>
                  <span>{candidate.organizations.join('; ') || 'Contributing organization not listed'}</span>
                  <span>{candidate.publishedAtUtc ? formatEventTime(candidate.publishedAtUtc) : 'Publication date not listed'}{candidate.formats.length ? ` / ${candidate.formats.join(', ')}` : ''}</span>
                  {candidate.countries.length ? <span>{candidate.countries.join(', ')}</span> : null}
                  {candidate.disasters.length ? <span>{candidate.disasters.map((item) => item.name).join(', ')}</span> : null}
                  <a href={candidate.canonicalUrl} target="_blank" rel="noreferrer">View canonical report</a>
                  <small>Metadata via ReliefWeb</small>
                </div>
                <button type="button" disabled={isAttached || isAttaching} onClick={() => onAttach(event.id, candidate.reportId)}>
                  {isAttached ? 'Attached' : isAttaching ? 'Attaching...' : 'Attach'}
                </button>
              </article>
            );
          })}
        </div>
      ) : null}
    </section>
  );
}
