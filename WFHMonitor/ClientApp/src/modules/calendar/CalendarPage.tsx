import { useMemo, useState, type FormEvent } from "react";
import { PageHeader } from "../../components/DataDisplay";
import {
  EmptyState,
  ErrorState,
  LoadingState,
  Notice
} from "../../components/PageStates";
import { ServerAction } from "../../components/ServerAction";
import { usePage } from "../../hooks/usePage";
import {
  ApiError,
  apiRoutes,
  postForm
} from "../../services/microservices";
import { useSession } from "../../state/SessionContext";
import type { UnknownRecord } from "../../types/api";

interface CalendarEvent {
  id: number;
  title: string;
  details?: string | null;
  meetingLink?: string | null;
  startAt: string;
  endAt?: string | null;
}

function monthLabel(year: number, month: number) {
  return new Date(year, month - 1, 1).toLocaleDateString([], {
    month: "long",
    year: "numeric"
  });
}

export function CalendarPage() {
  const today = useMemo(() => new Date(), []);
  const [year, setYear] = useState(today.getFullYear());
  const [month, setMonth] = useState(today.getMonth() + 1);
  const page = usePage<CalendarEvent[]>(apiRoutes.calendar.events(year, month));
  const { session } = useSession();
  const [showCreate, setShowCreate] = useState(false);
  const [icsUrl, setIcsUrl] = useState("");
  const [values, setValues] = useState<UnknownRecord>({
    title: "",
    details: "",
    meetingLink: "",
    startAt: "",
    endAt: "",
    returnUrl: "/app/calendar"
  });
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function moveMonth(delta: number) {
    const next = new Date(year, month - 1 + delta, 1);
    setYear(next.getFullYear());
    setMonth(next.getMonth() + 1);
  }

  async function createEvent(event: FormEvent) {
    event.preventDefault();
    if (!session) return;
    try {
      setBusy(true);
      setError(null);
      const response = await postForm(
        apiRoutes.calendar.create,
        values,
        session.antiForgeryToken
      );
      setMessage(response.success ?? "Calendar event added.");
      setShowCreate(false);
      setValues({
        title: "",
        details: "",
        meetingLink: "",
        startAt: "",
        endAt: "",
        returnUrl: "/app/calendar"
      });
      await page.reload();
    } catch (caught) {
      setError(
        caught instanceof ApiError
          ? caught.message
          : "The event could not be created."
      );
    } finally {
      setBusy(false);
    }
  }

  const complete = (fallback: string) => () => {
    setMessage(fallback);
    void page.reload();
  };

  return (
    <>
      <PageHeader
        eyebrow="Schedule"
        title="Team calendar"
        description="Plan delivery events and optionally import an Outlook ICS feed."
        actions={
          <>
            <a
              className="button button-tertiary"
              href={apiRoutes.calendar.feedIcs}
            >
              <i className="bi bi-download" /> Export ICS
            </a>
            <button
              className="button button-primary"
              type="button"
              onClick={() => setShowCreate((open) => !open)}
            >
              <i className="bi bi-plus-lg" /> New event
            </button>
          </>
        }
      />
      {message && (
        <Notice onDismiss={() => setMessage(null)}>{message}</Notice>
      )}
      {error && (
        <Notice kind="error" onDismiss={() => setError(null)}>
          {error}
        </Notice>
      )}
      {showCreate && (
        <form
          className="surface entity-form calendar-form"
          onSubmit={(event) => void createEvent(event)}
        >
          <div className="form-grid">
            <label className="form-field form-field-full">
              <span>Title</span>
              <input
                required
                maxLength={200}
                value={String(values.title)}
                onChange={(event) =>
                  setValues((current) => ({
                    ...current,
                    title: event.target.value
                  }))}
              />
            </label>
            <label className="form-field form-field-full">
              <span>Details</span>
              <textarea
                rows={3}
                value={String(values.details)}
                onChange={(event) =>
                  setValues((current) => ({
                    ...current,
                    details: event.target.value
                  }))}
              />
            </label>
            <label className="form-field">
              <span>Starts</span>
              <input
                required
                type="datetime-local"
                value={String(values.startAt)}
                onChange={(event) =>
                  setValues((current) => ({
                    ...current,
                    startAt: event.target.value
                  }))}
              />
            </label>
            <label className="form-field">
              <span>Ends</span>
              <input
                type="datetime-local"
                value={String(values.endAt)}
                onChange={(event) =>
                  setValues((current) => ({
                    ...current,
                    endAt: event.target.value
                  }))}
              />
            </label>
            <label className="form-field form-field-full">
              <span>Meeting link</span>
              <input
                type="url"
                value={String(values.meetingLink)}
                onChange={(event) =>
                  setValues((current) => ({
                    ...current,
                    meetingLink: event.target.value
                  }))}
              />
            </label>
          </div>
          <footer className="form-footer">
            <button
              className="button button-tertiary"
              type="button"
              onClick={() => setShowCreate(false)}
            >
              Cancel
            </button>
            <button
              className="button button-primary"
              disabled={busy}
              type="submit"
            >
              Add event
            </button>
          </footer>
        </form>
      )}
      <section className="calendar-layout">
        <article className="surface calendar-agenda">
          <header className="calendar-toolbar">
            <button
              className="icon-button"
              type="button"
              aria-label="Previous month"
              onClick={() => moveMonth(-1)}
            >
              <i className="bi bi-chevron-left" />
            </button>
            <h2>{monthLabel(year, month)}</h2>
            <button
              className="icon-button"
              type="button"
              aria-label="Next month"
              onClick={() => moveMonth(1)}
            >
              <i className="bi bi-chevron-right" />
            </button>
          </header>
          {page.loading ? (
            <LoadingState rows={4} />
          ) : page.error ? (
            <ErrorState message={page.error} onRetry={page.reload} />
          ) : (page.data?.length ?? 0) === 0 ? (
            <EmptyState description="No events are scheduled for this month." />
          ) : (
            <div className="agenda-list">
              {page.data?.map((item) => (
                <article key={item.id}>
                  <time dateTime={item.startAt}>
                    <strong>
                      {new Date(item.startAt).toLocaleDateString([], {
                        day: "2-digit"
                      })}
                    </strong>
                    <span>
                      {new Date(item.startAt).toLocaleDateString([], {
                        weekday: "short"
                      })}
                    </span>
                  </time>
                  <div>
                    <h3>{item.title}</h3>
                    <p>{item.details ?? "No additional details."}</p>
                    <small>
                      {new Date(item.startAt).toLocaleString()}
                      {item.endAt
                        ? ` – ${new Date(item.endAt).toLocaleString()}`
                        : ""}
                    </small>
                  </div>
                  <div className="agenda-actions">
                    {item.meetingLink && (
                      <a
                        href={item.meetingLink}
                        rel="noreferrer"
                        target="_blank"
                      >
                        Join
                      </a>
                    )}
                    <a href={apiRoutes.calendar.eventIcs(item.id)}>ICS</a>
                  </div>
                </article>
              ))}
            </div>
          )}
        </article>
        <aside className="surface calendar-connect">
          <span className="eyebrow">Outlook</span>
          <h2>Import a calendar</h2>
          <p>
            Paste a private ICS subscription URL. It stays on the server and is
            never returned by the API.
          </p>
          <label className="form-field">
            <span>ICS URL</span>
            <input
              type="url"
              placeholder="https://outlook.office365.com/..."
              value={icsUrl}
              onChange={(event) => setIcsUrl(event.target.value)}
            />
          </label>
          <ServerAction
            endpoint={apiRoutes.calendar.connectOutlook}
            values={{ icsUrl, returnUrl: "/app/calendar" }}
            className="button button-secondary"
            onComplete={complete("Outlook calendar connected.")}
          >
            Connect feed
          </ServerAction>
          <ServerAction
            endpoint={apiRoutes.calendar.syncOutlook}
            values={{ returnUrl: "/app/calendar" }}
            onComplete={complete("Outlook sync completed.")}
          >
            Sync now
          </ServerAction>
          <ServerAction
            endpoint={apiRoutes.calendar.disconnectOutlook}
            values={{ returnUrl: "/app/calendar" }}
            className="text-button danger"
            confirmMessage="Disconnect the Outlook calendar feed?"
            onComplete={complete("Outlook calendar disconnected.")}
          >
            Disconnect
          </ServerAction>
        </aside>
      </section>
    </>
  );
}
