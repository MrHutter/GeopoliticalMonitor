import { useCallback, useEffect, useState } from 'react';
import { sendAccentPreference } from '../nativeBridge';
import type { Accent } from '../types/mock';

export function useAccentPreference() {
  const [accent, setAccentState] = useState<Accent>('orange');
  const [persistenceError, setPersistenceError] = useState<string | null>(null);

  useEffect(() => {
    document.documentElement.dataset.accent = accent;
  }, [accent]);

  const setAccent = useCallback((nextAccent: Accent) => {
    setAccentState(nextAccent);
    setPersistenceError(null);
    sendAccentPreference(nextAccent);
  }, []);

  const applyPersistedAccent = useCallback((nextAccent: Accent) => {
    setAccentState(nextAccent);
    setPersistenceError(null);
  }, []);

  const reportPersistenceError = useCallback((message: string) => {
    setPersistenceError(message);
  }, []);

  return { accent, setAccent, applyPersistedAccent, persistenceError, reportPersistenceError };
}
