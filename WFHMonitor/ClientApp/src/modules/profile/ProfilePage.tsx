import { useState, type FormEvent } from "react";
import { PageHeader, StatusChip } from "../../components/DataDisplay";
import { Notice } from "../../components/PageStates";
import {
  ApiError,
  apiRoutes,
  postForm
} from "../../services/microservices";
import { useSession } from "../../state/SessionContext";

export function ProfilePage() {
  const { session, refresh } = useSession();
  const [fullName, setFullName] = useState(session?.user?.name ?? "");
  const [photo, setPhoto] = useState<File | undefined>();
  const [preview, setPreview] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  function selectPhoto(file: File | undefined) {
    if (preview) URL.revokeObjectURL(preview);
    setPhoto(file);
    setPreview(file ? URL.createObjectURL(file) : null);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!session) return;
    try {
      setBusy(true);
      setError(null);
      const response = await postForm(
        apiRoutes.auth.updateProfile,
        {
          fullName,
          profilePhoto: photo,
          returnUrl: "/app/profile"
        },
        session.antiForgeryToken,
        true
      );
      setMessage(response.success ?? "Profile updated.");
      await refresh();
    } catch (caught) {
      setError(
        caught instanceof ApiError
          ? caught.message
          : "Your profile could not be updated."
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <PageHeader
        eyebrow="Account"
        title="Your profile"
        description="Keep your identity recognizable across projects, teams, and activity."
      />
      {message && (
        <Notice onDismiss={() => setMessage(null)}>{message}</Notice>
      )}
      {error && (
        <Notice kind="error" onDismiss={() => setError(null)}>
          {error}
        </Notice>
      )}
      <form
        className="surface profile-form"
        onSubmit={(event) => void submit(event)}
      >
        <div className="profile-photo-editor">
          <span className="profile-photo-preview">
            {preview || session?.user?.photoUrl ? (
              <img
                src={preview ?? session?.user?.photoUrl ?? ""}
                alt="Profile preview"
              />
            ) : (
              fullName.slice(0, 2).toUpperCase()
            )}
          </span>
          <label className="button button-secondary">
            <i className="bi bi-camera" aria-hidden="true" />
            Choose photo
            <input
              accept=".jpg,.jpeg,.png,.webp,.gif,image/*"
              type="file"
              onChange={(event) => selectPhoto(event.target.files?.[0])}
            />
          </label>
          <small>JPG, PNG, WebP, or GIF.</small>
        </div>
        <div className="profile-fields">
          <label className="form-field">
            <span>Full name</span>
            <input
              maxLength={100}
              required
              value={fullName}
              onChange={(event) => setFullName(event.target.value)}
            />
          </label>
          <label className="form-field">
            <span>Email</span>
            <input disabled value={session?.user?.email ?? ""} />
            <small>Email is managed by your workspace administrator.</small>
          </label>
          <div className="profile-meta">
            <StatusChip value={session?.user?.plan ?? "Free"} />
            <span>
              {session?.user?.companyName ?? "Personal workspace"}
            </span>
          </div>
          <button
            className="button button-primary"
            disabled={busy}
            type="submit"
          >
            {busy && (
              <i
                className="bi bi-arrow-repeat spin"
                aria-hidden="true"
              />
            )}
            Save profile
          </button>
        </div>
      </form>
    </>
  );
}
