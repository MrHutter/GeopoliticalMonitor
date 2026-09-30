export type NativeEventCategory =
  | 'MilitaryConflict'
  | 'Security'
  | 'PoliticalDiplomatic'
  | 'CivilUnrest'
  | 'Infrastructure'
  | 'Humanitarian'
  | 'NaturalDisaster'
  | 'Economic'
  | 'Other';

export type NativeEventStatus = 'Developing' | 'Active' | 'Resolved' | 'Unverified';

export type NativeEventSeverity = 'Low' | 'Medium' | 'High' | 'Critical' | 'Catastrophic';

export type NativeEventProvider = 'Gdacs' | 'UcdpCandidate' | 'Gdelt' | 'Demo' | 'Manual';

export type NativeSourceType = 'XPost' | 'NewsArticle' | 'OfficialStatement' | 'DatasetRecord' | 'Other';

export type NativeLocation = {
  latitude: number;
  longitude: number;
  displayName: string;
  countryCode?: string | null;
  region?: string | null;
};

export type NativeEvent = {
  id: string;
  title: string;
  summary: string;
  category: NativeEventCategory;
  status: NativeEventStatus;
  startedAtUtc: string;
  lastUpdatedAtUtc: string;
  location: NativeLocation;
  createdAtUtc: string;
  updatedAtUtc: string;
  sourceCount: number;
  severity: NativeEventSeverity;
  severityRule: string;
  severityInputs: string;
  provider: NativeEventProvider;
};

export type NativeSourceItem = {
  id: string;
  sourceType: NativeSourceType;
  externalId?: string | null;
  title?: string | null;
  text: string;
  url?: string | null;
  authorDisplayName?: string | null;
  authorHandle?: string | null;
  publishedAtUtc: string;
  retrievedAtUtc: string;
  location?: NativeLocation | null;
};

export type ReliefWebDisaster = {
  id: string;
  name: string;
  glide?: string | null;
  types: string[];
};

export type ReliefWebReportCandidate = {
  reportId: string;
  title: string;
  canonicalUrl: string;
  publishedAtUtc?: string | null;
  organizations: string[];
  formats: string[];
  countries: string[];
  disasters: ReliefWebDisaster[];
  languages: string[];
};

export function formatCategory(category: NativeEventCategory) {
  switch (category) {
    case 'MilitaryConflict':
      return 'Military conflict';
    case 'PoliticalDiplomatic':
      return 'Political / diplomatic';
    case 'CivilUnrest':
      return 'Civil unrest';
    case 'NaturalDisaster':
      return 'Natural Disaster';
    default:
      return category;
  }
}

export const eventSeverities: readonly NativeEventSeverity[] = ['Low', 'Medium', 'High', 'Critical', 'Catastrophic'];

export function severityRank(severity: NativeEventSeverity) {
  return eventSeverities.indexOf(severity);
}

export function formatProviderLabel(provider: NativeEventProvider): string | null {
  switch (provider) {
    case 'Gdacs':
      return 'GDACS';
    case 'UcdpCandidate':
      return 'UCDP Candidate';
    case 'Gdelt':
      return 'GDELT · machine-coded';
    case 'Demo':
      return 'Demo';
    default:
      return null;
  }
}

export type SeverityExplanation = {
  rule: string;
  derivedFromProvider: boolean;
};

export function explainSeverity(event: Pick<NativeEvent, 'severity' | 'severityRule' | 'severityInputs'>): SeverityExplanation {
  switch (event.severityRule) {
    case 'gdacs-eq-v1': {
      const alert = /(?:^|;\s*)alert=([A-Za-z]+)/.exec(event.severityInputs)?.[1];
      const magnitude = /(?:^|;\s*)magnitude=([0-9.]+)/.exec(event.severityInputs)?.[1];
      const alertText = alert ? `GDACS ${alert} alert` : 'GDACS alert';
      return { rule: `${alertText}${magnitude ? `, magnitude ${magnitude}` : ''}, ${event.severity}`, derivedFromProvider: true };
    }
    case 'manual':
      return { rule: `Set manually, not derived from provider data (${event.severityInputs}).`, derivedFromProvider: false };
    case 'legacy-default':
      return { rule: 'Legacy default: no provider data was available to derive a severity when this Event was upgraded.', derivedFromProvider: false };
    default:
      return { rule: `Rule ${event.severityRule}, ${event.severity}`, derivedFromProvider: true };
  }
}

export function formatSourceType(sourceType: NativeSourceType) {
  switch (sourceType) {
    case 'XPost':
      return 'X';
    case 'NewsArticle':
      return 'NEWS';
    case 'OfficialStatement':
      return 'OFFICIAL';
    case 'DatasetRecord':
      return 'DATASET';
    default:
      return 'OTHER';
  }
}

export function formatEventTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString(undefined, {
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatCoordinates(location: NativeLocation) {
  return `${location.longitude.toFixed(1)} lon / ${location.latitude.toFixed(1)} lat`;
}
