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
