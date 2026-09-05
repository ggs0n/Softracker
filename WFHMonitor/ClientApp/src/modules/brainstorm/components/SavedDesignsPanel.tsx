import type {
  DesignProjectDetails,
  DesignProjectSummary
} from "../types";

interface SavedDesignsPanelProps {
  designs: DesignProjectSummary[];
  selectedDesign: DesignProjectDetails | null;
  isLoading: boolean;
  onRefresh: () => void;
  onLoad: (id: number) => void;
  onDelete: (id: number) => void;
}

export function SavedDesignsPanel({
  designs,
  selectedDesign,
  isLoading,
  onRefresh,
  onLoad,
  onDelete
}: SavedDesignsPanelProps) {
  return (
    <section className="bsm-card bsm-history-card" aria-labelledby="saved-designs-title">
      <header className="bsm-panel-heading">
        <div>
          <span className="bsm-kicker">Library</span>
          <h2 id="saved-designs-title">Saved blueprints</h2>
        </div>
        <div className="bsm-heading-actions">
          <span>{isLoading ? "Loading…" : `${designs.length} saved`}</span>
          <button
            className="bsm-icon-button"
            type="button"
            title="Refresh saved blueprints"
            aria-label="Refresh saved blueprints"
            onClick={onRefresh}
          >
            <i className="bi bi-arrow-clockwise" />
          </button>
        </div>
      </header>

      <div className="bsm-history-list">
        {designs.length === 0 && !isLoading ? (
          <div className="bsm-empty-list">
            <i className="bi bi-journal-plus" />
            <p>No blueprints saved yet.</p>
          </div>
        ) : (
          designs.map((design) => (
            <article
              className={`bsm-history-item${selectedDesign?.id === design.id ? " selected" : ""}`}
              key={design.id}
            >
              <button type="button" onClick={() => onLoad(design.id)}>
                <span className="bsm-history-source">{design.sourceMode}</span>
                <strong>{design.title}</strong>
                <p>{design.technology}</p>
                <small>
                  {design.userCount} · {formatDate(design.createdAt)}
                </small>
              </button>
              <button
                className="bsm-delete-button"
                type="button"
                title={`Delete ${design.title}`}
                aria-label={`Delete ${design.title}`}
                onClick={() => onDelete(design.id)}
              >
                <i className="bi bi-trash3" />
              </button>
            </article>
          ))
        )}
      </div>
    </section>
  );
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}
