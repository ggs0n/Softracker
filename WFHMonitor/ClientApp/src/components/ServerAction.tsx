import { useState, type ReactNode } from "react";
import { ApiError, postForm } from "../services/microservices";
import { useSession } from "../state/SessionContext";
import type { ApiEnvelope, UnknownRecord } from "../types/api";

export function ServerAction({
  endpoint,
  values = {},
  children,
  className = "button button-secondary",
  confirmMessage,
  onComplete
}: {
  endpoint: string;
  values?: UnknownRecord;
  children: ReactNode;
  className?: string;
  confirmMessage?: string;
  onComplete?: (response: ApiEnvelope<unknown>) => void;
}) {
  const { session, refresh } = useSession();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function execute() {
    if (confirmMessage && !window.confirm(confirmMessage)) return;
    if (!session) return;

    try {
      setBusy(true);
      setError(null);
      const response = await postForm(endpoint, values, session.antiForgeryToken);
      if (response.redirect?.url?.startsWith("http")) {
        window.location.assign(response.redirect.url);
        return;
      }
      await refresh();
      onComplete?.(response);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "The operation failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <span className="server-action">
      <button className={className} type="button" disabled={busy} onClick={execute}>
        {busy && <i className="bi bi-arrow-repeat spin" aria-hidden="true" />}
        {children}
      </button>
      {error && <small role="alert">{error}</small>}
    </span>
  );
}
