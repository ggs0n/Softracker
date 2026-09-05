import type { AiAccountStatus, GenerateDesignRequest } from "../types";

interface BrainstormInputPanelProps {
  form: GenerateDesignRequest;
  chatGptResult: string;
  copyStatus: string | null;
  error: string | null;
  isGenerating: boolean;
  isImporting: boolean;
  isFormValid: boolean;
  isAdmin: boolean;
  aiAccount: AiAccountStatus | null;
  isAccountLoading: boolean;
  isAccountLoginPending: boolean;
  onFieldChange: (field: keyof GenerateDesignRequest, value: string) => void;
  onChatGptResultChange: (value: string) => void;
  onGenerate: () => void;
  onCopyPrompt: () => void;
  onImport: () => void;
  onUseExample: () => void;
  onClear: () => void;
  onConnectAccount: () => void;
  onCancelAccountLogin: () => void;
  onLogoutAccount: () => void;
}

export function BrainstormInputPanel({
  form,
  chatGptResult,
  copyStatus,
  error,
  isGenerating,
  isImporting,
  isFormValid,
  isAdmin,
  aiAccount,
  isAccountLoading,
  isAccountLoginPending,
  onFieldChange,
  onChatGptResultChange,
  onGenerate,
  onCopyPrompt,
  onImport,
  onUseExample,
  onClear,
  onConnectAccount,
  onCancelAccountLogin,
  onLogoutAccount
}: BrainstormInputPanelProps) {
  return (
    <section className="bsm-card bsm-input-card" aria-labelledby="brainstorm-input-title">
      <header className="bsm-card-heading">
        <span className="bsm-heading-icon"><i className="bi bi-bezier2" /></span>
        <div>
          <span className="bsm-kicker">Design brief</span>
          <h2 id="brainstorm-input-title">Shape the system</h2>
          <p>Describe the product, stack, audience, and essential workflows.</p>
        </div>
      </header>

      <div className={`bsm-account ${aiAccount?.state === "connected" ? "connected" : ""}`}>
        <span className="bsm-account-icon">
          <i className={`bi ${aiAccount?.state === "connected" ? "bi-check-circle" : "bi-person-lock"}`} />
        </span>
        <div>
          <strong>
            {aiAccount?.state === "connected"
              ? "ChatGPT connected"
              : isAdmin
                ? "Connect ChatGPT Plus"
                : "Administrator connection required"}
          </strong>
          <small>
            {aiAccount?.state === "connected"
              ? `${aiAccount.email ?? "ChatGPT account"} · ${aiAccount.model}`
              : aiAccount?.message ?? "Codex OAuth is available to administrators only."}
          </small>
          {aiAccount?.usedPercent !== null && aiAccount?.usedPercent !== undefined && (
            <small>{aiAccount.usedPercent}% of the current Codex limit used</small>
          )}
        </div>
        {isAdmin && aiAccount?.state === "connected" ? (
          <button type="button" className="bsm-account-action" disabled={isAccountLoading} onClick={onLogoutAccount}>
            Disconnect
          </button>
        ) : isAdmin && isAccountLoginPending ? (
          <button type="button" className="bsm-account-action" onClick={onCancelAccountLogin}>
            Cancel
          </button>
        ) : isAdmin ? (
          <button type="button" className="bsm-account-action" disabled={isAccountLoading} onClick={onConnectAccount}>
            {isAccountLoading ? "Checking" : "Connect"}
          </button>
        ) : null}
      </div>

      <form
        className="bsm-form"
        onSubmit={(event) => {
          event.preventDefault();
          onGenerate();
        }}
      >
        <TextAreaField
          label="Simple summary"
          value={form.summary}
          placeholder="Food delivery app for customers, restaurants, and riders"
          rows={3}
          onChange={(value) => onFieldChange("summary", value)}
        />
        <TextAreaField
          label="Technology"
          value={form.technology}
          placeholder="React, ASP.NET Core, SQL Server, Redis"
          rows={2}
          onChange={(value) => onFieldChange("technology", value)}
        />
        <TextAreaField
          label="Cloud / hosting target"
          value={form.cloudHostingTarget}
          placeholder="Optional: Azure, AWS, on-prem IIS, Docker VPS"
          rows={2}
          required={false}
          onChange={(value) => onFieldChange("cloudHostingTarget", value)}
        />
        <TextAreaField
          label="Expected users"
          value={form.userCount}
          placeholder="10,000 users"
          rows={1}
          onChange={(value) => onFieldChange("userCount", value)}
        />
        <TextAreaField
          label="Main features"
          value={form.features}
          placeholder="Ordering, payment, tracking, notifications"
          rows={3}
          onChange={(value) => onFieldChange("features", value)}
        />

        <div className="bsm-form-actions">
          <button
            className="button button-primary"
            type="submit"
            disabled={!isFormValid || isGenerating || !isAdmin || aiAccount?.state !== "connected"}
          >
            <i className={`bi ${isGenerating ? "bi-arrow-repeat bsm-spin" : "bi-play-fill"}`} />
            {isGenerating ? "Generating" : "Generate blueprint"}
          </button>
          <button
            className="button button-secondary"
            type="button"
            disabled={!isFormValid}
            onClick={onCopyPrompt}
          >
            <i className="bi bi-copy" />
            ChatGPT prompt
          </button>
          <button
            className="bsm-icon-button"
            type="button"
            title="Use sample input"
            aria-label="Use sample input"
            onClick={onUseExample}
          >
            <i className="bi bi-lightning-charge" />
          </button>
          <button
            className="bsm-icon-button"
            type="button"
            title="Clear input"
            aria-label="Clear input"
            onClick={onClear}
          >
            <i className="bi bi-arrow-counterclockwise" />
          </button>
        </div>
      </form>

      <details className="bsm-import-panel">
        <summary>
          <span><i className="bi bi-stars" /> ChatGPT Plus import</span>
          <small>Manual JSON mode</small>
        </summary>
        <div className="bsm-import-body">
          <p>Copy the prepared prompt into ChatGPT, then paste its JSON response here to validate, render, and save it.</p>
          {copyStatus && <p className="bsm-copy-status">{copyStatus}</p>}
          <textarea
            value={chatGptResult}
            rows={6}
            aria-label="ChatGPT JSON response"
            placeholder="Paste ChatGPT JSON response"
            onChange={(event) => onChatGptResultChange(event.target.value)}
          />
          <button
            className="button button-secondary"
            type="button"
            disabled={
              !isFormValid ||
              chatGptResult.trim().length === 0 ||
              isImporting
            }
            onClick={onImport}
          >
            <i className={`bi ${isImporting ? "bi-arrow-repeat bsm-spin" : "bi-box-arrow-in-down"}`} />
            {isImporting ? "Importing" : "Save imported blueprint"}
          </button>
        </div>
      </details>

      {error && (
        <div className="bsm-alert" role="alert">
          <i className="bi bi-exclamation-triangle" />
          <span>{error}</span>
        </div>
      )}
    </section>
  );
}

interface TextAreaFieldProps {
  label: string;
  value: string;
  placeholder: string;
  rows: number;
  required?: boolean;
  onChange: (value: string) => void;
}

function TextAreaField({
  label,
  value,
  placeholder,
  rows,
  required = true,
  onChange
}: TextAreaFieldProps) {
  return (
    <label className="bsm-field">
      <span>{label}{!required && <small>Optional</small>}</span>
      <textarea
        required={required}
        rows={rows}
        value={value}
        placeholder={placeholder}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}
