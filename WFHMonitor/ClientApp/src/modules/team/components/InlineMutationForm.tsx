import { useState, type Dispatch, type FormEvent, type SetStateAction } from "react";
import { ApiError, postForm } from "../../../services/microservices";
import { useSession } from "../../../state/SessionContext";
import type {
  ApiEnvelope,
  UnknownRecord
} from "../../../types/api";

interface InlineField {
  name: string;
  label: string;
  options?: Array<{ value: string; text: string }>;
  placeholder?: string;
}

interface InlineMutationFormProps {
  endpoint: string;
  values: UnknownRecord;
  setValues: Dispatch<SetStateAction<UnknownRecord>>;
  fields: InlineField[];
  submitLabel: string;
  onComplete: (response: ApiEnvelope<unknown>) => void;
  prefix?: string;
  confirmMessage?: string;
}

export function InlineMutationForm({
  endpoint,
  values,
  setValues,
  fields,
  submitLabel,
  onComplete,
  prefix,
  confirmMessage
}: InlineMutationFormProps) {
  const { session } = useSession();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!session) return;
    if (confirmMessage && !window.confirm(confirmMessage)) return;
    try {
      setBusy(true);
      setError(null);
      const payload = prefix ? { [prefix]: values } : values;
      const response = await postForm(
        endpoint,
        payload,
        session.antiForgeryToken
      );
      onComplete(response);
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : "The operation failed."
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <form
      className="inline-form"
      onSubmit={(event) => void submit(event)}
    >
      {fields.map((field) => (
        <label key={field.name}>
          <span>{field.label}</span>
          {field.options ? (
            <select
              value={String(values[field.name] ?? "")}
              onChange={(event) =>
                setValues((current) => ({
                  ...current,
                  [field.name]: event.target.value
                }))}
            >
              <option value="">Select</option>
              {field.options.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.text}
                </option>
              ))}
            </select>
          ) : (
            <input
              placeholder={field.placeholder}
              value={String(values[field.name] ?? "")}
              onChange={(event) =>
                setValues((current) => ({
                  ...current,
                  [field.name]: event.target.value
                }))}
            />
          )}
        </label>
      ))}
      <button
        className="button button-secondary"
        disabled={busy}
        type="submit"
      >
        {submitLabel}
      </button>
      {error && <small className="field-error">{error}</small>}
    </form>
  );
}
