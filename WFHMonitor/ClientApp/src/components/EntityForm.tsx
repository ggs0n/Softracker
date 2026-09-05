import { useEffect, useMemo, useState, type FormEvent } from "react";
import { ApiError, postForm } from "../services/microservices";
import { usePage } from "../hooks/usePage";
import { Link, useNavigate, useParams, useSearchParams } from "../state/RouterContext";
import { useSession } from "../state/SessionContext";
import type { SelectOption, UnknownRecord } from "../types/api";
import { PageHeader } from "./DataDisplay";
import { ErrorState, LoadingState, Notice } from "./PageStates";

export type FieldType =
  | "text"
  | "textarea"
  | "date"
  | "url"
  | "select"
  | "checkbox"
  | "file";

export interface FormField {
  name: string;
  label: string;
  type?: FieldType;
  required?: boolean;
  maxLength?: number;
  span?: "full";
  help?: string;
  options?: SelectOption[];
  optionsFrom?: string;
}

export interface FormDefinition {
  eyebrow: string;
  createTitle: string;
  editTitle: string;
  description: string;
  getEndpoint: (id: string | undefined, projectId: string | null) => string;
  postEndpoint: (id: string | undefined) => string;
  listPath: string;
  detailPath: (id: string) => string;
  fields: FormField[];
  additionalValues?: UnknownRecord;
}

export const enumOptions = (...values: string[]): SelectOption[] =>
  values.map((value) => ({
    value,
    text: value.replace(/([a-z])([A-Z])/g, "$1 $2")
  }));

function getOptions(model: UnknownRecord, field: FormField): SelectOption[] {
  if (field.options) return field.options;
  if (!field.optionsFrom) return [];
  const source = model[field.optionsFrom];
  if (!Array.isArray(source)) return [];
  return source.map((option) => {
    const record = option as UnknownRecord;
    return {
      value: String(record.value ?? record.id ?? ""),
      text: String(record.text ?? record.title ?? record.name ?? record.value ?? ""),
      selected: Boolean(record.selected),
      disabled: Boolean(record.disabled)
    };
  });
}

function initialFieldValue(field: FormField, model: UnknownRecord): unknown {
  const value = model[field.name];
  if (field.type === "checkbox") return Boolean(value);
  if (field.type === "date" && typeof value === "string") return value.slice(0, 10);
  if (field.type === "file") return undefined;
  return value ?? "";
}

function findFieldError(
  errors: Record<string, string[]>,
  name: string
): string | undefined {
  const match = Object.entries(errors).find(
    ([key]) => key.toLowerCase().split(".").at(-1) === name.toLowerCase()
  );
  return match?.[1]?.[0];
}

export function EntityForm({ definition }: { definition: FormDefinition }) {
  const { id } = useParams();
  const [search] = useSearchParams();
  const projectId = search.get("projectId");
  const endpoint = definition.getEndpoint(id, projectId);
  const page = usePage<UnknownRecord>(endpoint);
  const { session } = useSession();
  const navigate = useNavigate();
  const [values, setValues] = useState<UnknownRecord>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (!page.data) return;
    const nextValues = Object.fromEntries(
      definition.fields.map((field) => [
        field.name,
        initialFieldValue(field, page.data!)
      ])
    );
    if (!id && projectId && "changeRequestId" in nextValues)
      nextValues.changeRequestId = projectId;
    setValues({
      ...nextValues,
      ...(id ? { id, featureId: id } : {}),
      ...definition.additionalValues
    });
  }, [definition, id, page.data, projectId]);

  const hasFile = useMemo(
    () => definition.fields.some((field) => field.type === "file"),
    [definition.fields]
  );

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!session) return;
    try {
      setBusy(true);
      setError(null);
      setFieldErrors({});
      const response = await postForm(
        definition.postEndpoint(id),
        values,
        session.antiForgeryToken,
        hasFile
      );
      const redirectValues = response.redirect?.routeValues as
        | UnknownRecord
        | undefined;
      const createdId = String(
        redirectValues?.id
          ?? redirectValues?.featureId
          ?? values.id
          ?? values.featureId
          ?? ""
      );
      navigate(createdId ? definition.detailPath(createdId) : definition.listPath);
    } catch (caught) {
      if (caught instanceof ApiError) {
        setError(caught.message);
        setFieldErrors(caught.errors);
      } else {
        setError("The record could not be saved.");
      }
    } finally {
      setBusy(false);
    }
  }

  if (page.loading) return <LoadingState />;
  if (page.error)
    return <ErrorState message={page.error} onRetry={page.reload} />;
  const model = page.data ?? {};

  return (
    <>
      <PageHeader
        eyebrow={definition.eyebrow}
        title={id ? definition.editTitle : definition.createTitle}
        description={definition.description}
      />
      <form
        className="surface entity-form"
        onSubmit={(event) => void submit(event)}
      >
        {error && <Notice kind="error">{error}</Notice>}
        <div className="form-grid">
          {definition.fields.map((field) => {
            const fieldError = findFieldError(fieldErrors, field.name);
            const className =
              field.span === "full"
                ? "form-field form-field-full"
                : "form-field";
            const stringValue = String(values[field.name] ?? "");
            return (
              <label className={className} key={field.name}>
                <span>
                  {field.label}
                  {field.required && <b aria-hidden="true"> *</b>}
                </span>
                {field.type === "textarea" ? (
                  <textarea
                    value={stringValue}
                    maxLength={field.maxLength}
                    required={field.required}
                    rows={5}
                    onChange={(event) =>
                      setValues((current) => ({
                        ...current,
                        [field.name]: event.target.value
                      }))}
                  />
                ) : field.type === "select" ? (
                  <select
                    value={stringValue}
                    required={field.required}
                    onChange={(event) =>
                      setValues((current) => ({
                        ...current,
                        [field.name]: event.target.value
                      }))}
                  >
                    <option value="">
                      Select {field.label.toLowerCase()}
                    </option>
                    {getOptions(model, field).map((option) => (
                      <option
                        disabled={option.disabled}
                        key={option.value}
                        value={option.value}
                      >
                        {option.text}
                      </option>
                    ))}
                  </select>
                ) : field.type === "checkbox" ? (
                  <input
                    type="checkbox"
                    checked={Boolean(values[field.name])}
                    onChange={(event) =>
                      setValues((current) => ({
                        ...current,
                        [field.name]: event.target.checked
                      }))}
                  />
                ) : field.type === "file" ? (
                  <input
                    type="file"
                    onChange={(event) =>
                      setValues((current) => ({
                        ...current,
                        [field.name]: event.target.files?.[0]
                      }))}
                  />
                ) : (
                  <input
                    type={field.type ?? "text"}
                    value={stringValue}
                    maxLength={field.maxLength}
                    required={field.required}
                    onChange={(event) =>
                      setValues((current) => ({
                        ...current,
                        [field.name]: event.target.value
                      }))}
                  />
                )}
                {field.help && <small>{field.help}</small>}
                {fieldError && (
                  <small className="field-error">{fieldError}</small>
                )}
              </label>
            );
          })}
        </div>
        <footer className="form-footer">
          <Link className="button button-tertiary" to={definition.listPath}>
            Cancel
          </Link>
          <button className="button button-primary" type="submit" disabled={busy}>
            {busy && (
              <i
                className="bi bi-arrow-repeat spin"
                aria-hidden="true"
              />
            )}
            {id ? "Save changes" : "Create"}
          </button>
        </footer>
      </form>
    </>
  );
}
