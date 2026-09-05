import { useCallback, useEffect, useState } from "react";
import { ApiError, getPage } from "../services/microservices";

export function usePage<T>(endpoint: string | null) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(
    async (signal?: AbortSignal) => {
      if (!endpoint) {
        setLoading(false);
        setError(null);
        setData(null);
        return;
      }
      try {
        setLoading(true);
        setError(null);
        const response = await getPage<T>(endpoint, signal);
        setData(response.data);
        setNotice(response.success ?? null);
      } catch (caught) {
        if (caught instanceof DOMException && caught.name === "AbortError") return;
        setError(
          caught instanceof ApiError || caught instanceof Error
            ? caught.message
            : "The page could not be loaded."
        );
      } finally {
        setLoading(false);
      }
    },
    [endpoint]
  );

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  return {
    data,
    loading,
    error,
    notice,
    reload: () => load()
  };
}
