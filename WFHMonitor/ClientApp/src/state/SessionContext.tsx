import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren
} from "react";
import { getBootstrap } from "../services/microservices";
import type { BootstrapResponse } from "../types/api";

interface SessionState {
  session: BootstrapResponse | null;
  loading: boolean;
  error: string | null;
  refresh: () => Promise<BootstrapResponse | null>;
}

const SessionContext = createContext<SessionState | null>(null);

export function SessionProvider({ children }: PropsWithChildren) {
  const [session, setSession] = useState<BootstrapResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setError(null);
      const nextSession = await getBootstrap();
      setSession(nextSession);
      return nextSession;
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "The session could not be loaded.");
      return null;
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const value = useMemo(
    () => ({ session, loading, error, refresh }),
    [session, loading, error, refresh]
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionState {
  const state = useContext(SessionContext);
  if (!state) throw new Error("useSession must be used inside SessionProvider.");
  return state;
}
