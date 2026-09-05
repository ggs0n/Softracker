import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Link } from "../state/RouterContext";
import type { UnknownRecord } from "../types/api";

export interface TableColumn<T> {
  key: keyof T | string;
  label: string;
  render?: (row: T) => ReactNode;
  className?: string;
}

function humanize(value: string): string {
  return value
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .replace(/^./, (letter) => letter.toUpperCase());
}

function isIsoDate(value: string): boolean {
  return /^\d{4}-\d{2}-\d{2}T/.test(value);
}

export function formatValue(value: unknown): ReactNode {
  if (value === null || value === undefined || value === "") return <span className="muted">—</span>;
  if (typeof value === "boolean")
    return <span>{value ? "Yes" : "No"}</span>;
  if (typeof value === "number")
    return <span className="number">{value.toLocaleString()}</span>;
  if (typeof value === "string") {
    if (isIsoDate(value))
      return (
        <time dateTime={value}>
          {new Intl.DateTimeFormat(undefined, {
            dateStyle: "medium",
            timeStyle: "short"
          }).format(new Date(value))}
        </time>
      );
    if (/^https?:\/\//i.test(value))
      return (
        <a href={value} target="_blank" rel="noreferrer">
          Open link <i className="bi bi-box-arrow-up-right" aria-hidden="true" />
        </a>
      );
    return value;
  }
  if (Array.isArray(value)) return `${value.length} item${value.length === 1 ? "" : "s"}`;
  if (typeof value === "object") {
    const record = value as UnknownRecord;
    return String(
      record.fullName ??
        record.name ??
        record.title ??
        record.crNumber ??
        record.email ??
        "View details"
    );
  }
  return String(value);
}

export function StatusChip({ value }: { value: string | null | undefined }) {
  const normalized = (value ?? "Unknown").toLowerCase().replace(/\s+/g, "-");
  const positive = ["done", "pass", "passed", "approved", "active", "healthy", "completed", "excellent"];
  const negative = ["blocked", "fail", "failed", "critical", "unhealthy", "rejected"];
  const warning = ["pending", "inprogress", "in-progress", "queued", "inreview", "at-risk", "high"];
  const tone = positive.includes(normalized)
    ? "positive"
    : negative.includes(normalized)
      ? "negative"
      : warning.includes(normalized)
        ? "warning"
        : "neutral";

  return <span className={`status status-${tone}`}>{humanize(value ?? "Unknown")}</span>;
}

export function PageHeader({
  eyebrow,
  title,
  description,
  actions
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <header className="page-header">
      <div>
        {eyebrow && <span className="eyebrow">{eyebrow}</span>}
        <h1>{title}</h1>
        {description && <p>{description}</p>}
      </div>
      {actions && <div className="page-actions">{actions}</div>}
    </header>
  );
}

export function MetricGrid({
  metrics
}: {
  metrics: Array<{
    label: string;
    value: ReactNode;
    hint?: string;
    icon?: string;
    tone?: "default" | "accent" | "danger";
  }>;
}) {
  return (
    <section className="metric-grid" aria-label="Summary">
      {metrics.map((metric) => (
        <article className={`metric metric-${metric.tone ?? "default"}`} key={metric.label}>
          <span className="metric-icon">
            <i className={`bi ${metric.icon ?? "bi-activity"}`} aria-hidden="true" />
          </span>
          <div>
            <span className="metric-label">{metric.label}</span>
            <strong>{metric.value}</strong>
            {metric.hint && <small>{metric.hint}</small>}
          </div>
        </article>
      ))}
    </section>
  );
}

export function DataTable<T extends object>({
  rows,
  columns,
  rowKey,
  linkForRow,
  emptyMessage = "No records match this view.",
  pageSize = 10
}: {
  rows: T[];
  columns: TableColumn<T>[];
  rowKey: (row: T) => string | number;
  linkForRow?: (row: T) => string;
  emptyMessage?: string;
  pageSize?: number;
}) {
  const [page, setPage] = useState(1);
  const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
  useEffect(() => {
    setPage((current) => Math.min(current, totalPages));
  }, [totalPages]);
  const visibleRows = useMemo(
    () => rows.slice((page - 1) * pageSize, page * pageSize),
    [page, pageSize, rows]
  );

  if (rows.length === 0)
    return <p className="table-empty">{emptyMessage}</p>;

  return (
    <>
      <div className="table-wrap">
        <table>
        <thead>
          <tr>
            {columns.map((column) => (
              <th className={column.className} key={String(column.key)}>
                {column.label}
              </th>
            ))}
            {linkForRow && <th><span className="visually-hidden">Open</span></th>}
          </tr>
        </thead>
        <tbody>
          {visibleRows.map((row) => {
            const record = row as unknown as UnknownRecord;
            const destination = linkForRow?.(row);
            return (
              <tr key={rowKey(row)}>
                {columns.map((column) => (
                  <td className={column.className} key={String(column.key)}>
                    {column.render
                      ? column.render(row)
                      : formatValue(record[String(column.key)])}
                  </td>
                ))}
                {destination && (
                  <td className="table-link">
                    <Link to={destination} aria-label="Open details">
                      <i className="bi bi-arrow-right" aria-hidden="true" />
                    </Link>
                  </td>
                )}
              </tr>
            );
          })}
        </tbody>
        </table>
      </div>
      {totalPages > 1 && (
        <nav className="table-pagination" aria-label="Table pages">
          <button type="button" disabled={page === 1} onClick={() => setPage((current) => current - 1)}>
            <i className="bi bi-arrow-left" aria-hidden="true" /> Previous
          </button>
          <span>Page {page} of {totalPages}</span>
          <button type="button" disabled={page === totalPages} onClick={() => setPage((current) => current + 1)}>
            Next <i className="bi bi-arrow-right" aria-hidden="true" />
          </button>
        </nav>
      )}
    </>
  );
}

export function DetailGrid({
  record,
  omit = []
}: {
  record: UnknownRecord;
  omit?: string[];
}) {
  const hidden = new Set([
    "id",
    "createdById",
    "assignedDeveloperId",
    "assignedAgentId",
    ...omit
  ]);
  const entries = Object.entries(record).filter(
    ([key, value]) =>
      !hidden.has(key) &&
      !Array.isArray(value) &&
      (typeof value !== "object" || value === null || ["createdBy", "assignedDeveloper", "assignedAgent", "changeRequest"].includes(key))
  );

  return (
    <dl className="detail-grid">
      {entries.map(([key, value]) => (
        <div key={key}>
          <dt>{humanize(key)}</dt>
          <dd>
            {["status", "stage", "priority", "severity", "agentStatus", "bugScanStatus"].includes(key)
              ? <StatusChip value={String(value ?? "Unknown")} />
              : formatValue(value)}
          </dd>
        </div>
      ))}
    </dl>
  );
}
