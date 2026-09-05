import { useState, type FormEvent } from "react";
import { ApiError, postForm } from "../services/microservices";
import { Notice } from "./PageStates";
import { ServerAction } from "./ServerAction";
import { useSession } from "../state/SessionContext";
import type { ApiEnvelope, UnknownRecord } from "../types/api";

export interface EvidenceItem extends UnknownRecord {
  id: number;
  fileName: string;
  originalFileName?: string | null;
  caption?: string | null;
  uploadedAt?: string | null;
}

export function EvidencePanel({
  title,
  eyebrow,
  items,
  fileBaseUrl,
  image,
  canManage,
  uploadEndpoint,
  uploadValues,
  deleteEndpoint,
  deleteValues,
  onComplete
}: {
  title: string;
  eyebrow: string;
  items: EvidenceItem[];
  fileBaseUrl: string;
  image?: boolean;
  canManage: boolean;
  uploadEndpoint: string;
  uploadValues: UnknownRecord;
  deleteEndpoint: string;
  deleteValues: (item: EvidenceItem) => UnknownRecord;
  onComplete: (response: ApiEnvelope<unknown>) => void;
}) {
  const { session } = useSession();
  const [file, setFile] = useState<File | undefined>();
  const [caption, setCaption] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function upload(event: FormEvent) {
    event.preventDefault();
    if (!session || !file) return;
    try {
      setBusy(true);
      setError(null);
      const response = await postForm(
        uploadEndpoint,
        { ...uploadValues, file, ...(image && caption ? { caption } : {}) },
        session.antiForgeryToken,
        true
      );
      setFile(undefined);
      setCaption("");
      onComplete(response);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "The file could not be uploaded.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="surface evidence-panel">
      <div className="section-heading">
        <div><span className="eyebrow">{eyebrow}</span><h2>{title}</h2></div>
        <span>{items.length} files</span>
      </div>
      {error && <Notice kind="error" onDismiss={() => setError(null)}>{error}</Notice>}
      {canManage && (
        <form className="evidence-upload" onSubmit={(event) => void upload(event)}>
          <label>
            <span>{image ? "Choose image" : "Choose document"}</span>
            <input
              accept={image ? "image/jpeg,image/png,image/gif,image/webp" : ".pdf,.docx,.xlsx,.xls,.pptx,.txt"}
              required
              type="file"
              onChange={(event) => setFile(event.target.files?.[0])}
            />
          </label>
          {image && "caption" in uploadValues && (
            <label><span>Caption</span><input maxLength={1000} value={caption} onChange={(event) => setCaption(event.target.value)} /></label>
          )}
          <button className="button button-secondary" disabled={busy || !file} type="submit">
            <i className="bi bi-upload" /> Upload
          </button>
        </form>
      )}
      {items.length === 0 ? (
        <p className="evidence-empty">No files have been added.</p>
      ) : image ? (
        <div className="evidence-gallery">
          {items.map((item) => (
            <article key={item.id}>
              <a href={`${fileBaseUrl}/${encodeURIComponent(item.fileName)}`} rel="noreferrer" target="_blank">
                <img src={`${fileBaseUrl}/${encodeURIComponent(item.fileName)}`} alt={item.originalFileName ?? item.caption ?? "Uploaded evidence"} />
              </a>
              <footer>
                <span title={item.originalFileName ?? item.fileName}>{item.caption ?? item.originalFileName ?? item.fileName}</span>
                {canManage && (
                  <ServerAction
                    endpoint={deleteEndpoint}
                    values={deleteValues(item)}
                    className="icon-button danger"
                    confirmMessage="Delete this file?"
                    onComplete={onComplete}
                  >
                    <i className="bi bi-trash" />
                  </ServerAction>
                )}
              </footer>
            </article>
          ))}
        </div>
      ) : (
        <div className="evidence-documents">
          {items.map((item) => (
            <article key={item.id}>
              <i className="bi bi-file-earmark-text" />
              <a href={`${fileBaseUrl}/${encodeURIComponent(item.fileName)}`} rel="noreferrer" target="_blank">{item.originalFileName ?? item.fileName}</a>
              <time dateTime={item.uploadedAt ?? undefined}>{item.uploadedAt ? new Date(item.uploadedAt).toLocaleDateString() : ""}</time>
              {canManage && (
                <ServerAction
                  endpoint={deleteEndpoint}
                  values={deleteValues(item)}
                  className="icon-button danger"
                  confirmMessage="Delete this file?"
                  onComplete={onComplete}
                >
                  <i className="bi bi-trash" />
                </ServerAction>
              )}
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
