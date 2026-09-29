export const accentOptions = [
  { value: 'orange', label: 'Orange' },
  { value: 'blue', label: 'Blue' },
  { value: 'red', label: 'Red' },
  { value: 'green', label: 'Green' },
  { value: 'yellow', label: 'Yellow' },
] as const;

export type Accent = (typeof accentOptions)[number]['value'];

export type MockXPost = {
  id: string;
  displayName: string;
  handle: string;
  timestamp: string;
  text: string;
  relatedEventId?: string;
  saved?: boolean;
};

export type MapLayer = {
  id: string;
  label: string;
  enabled: boolean;
  status?: 'active' | 'demo' | 'pending';
};
