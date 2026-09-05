import type { ReactNode } from "react";

export function LoadingState({ rows = 5 }: { rows?: number }) {
  return (
    <div className="surface skeleton-surface" aria-label="Loading">
      <div className="skeleton skeleton-title" />
      {Array.from({ length: rows }, (_, index) => (
        <div className="skeleton skeleton-row" key={index} />
      ))}
    </div>
  );
}

export function ErrorState({
  message,
  onRetry
}: {
  message: string;
  onRetry?: () => void;
}) {
  return (
    <section className="state-panel state-error" role="alert">
      <i className="bi bi-exclamation-diamond" aria-hidden="true" />
      <div>
        <h2>We couldn’t load this view</h2>
        <p>{message}</p>
      </div>
      {onRetry && (
        <button className="button button-secondary" type="button" onClick={onRetry}>
          Try again
        </button>
      )}
    </section>
  );
}

export function EmptyState({
  title = "Nothing here yet",
  description,
  action
}: {
  title?: string;
  description: string;
  action?: ReactNode;
}) {
  return (
    <section className="state-panel">
      <i className="bi bi-inbox" aria-hidden="true" />
      <div>
        <h2>{title}</h2>
        <p>{description}</p>
      </div>
      {action}
    </section>
  );
}

export function Notice({
  kind = "success",
  children,
  onDismiss
}: {
  kind?: "success" | "error" | "info";
  children: ReactNode;
  onDismiss?: () => void;
}) {
  return (
    <div className={`notice notice-${kind}`} role={kind === "error" ? "alert" : "status"}>
      <span>{children}</span>
      {onDismiss && (
        <button type="button" aria-label="Dismiss message" onClick={onDismiss}>
          <i className="bi bi-x" aria-hidden="true" />
        </button>
      )}
    </div>
  );
}
