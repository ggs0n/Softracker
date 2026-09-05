import { useEffect, useState } from "react";
import { PageHeader } from "../../components/DataDisplay";
import { PageBoundary } from "../../components/PageBoundary";
import { Notice } from "../../components/PageStates";
import { usePage } from "../../hooks/usePage";
import {
  ApiError,
  apiRoutes,
  postForm
} from "../../services/microservices";
import { useSession } from "../../state/SessionContext";
import type { UnknownRecord } from "../../types/api";

interface SettingsModel extends UnknownRecord {
  activeMenu: string;
  modules: Array<
    UnknownRecord & {
      moduleKey: string;
      displayName: string;
    }
  >;
  bellNotificationSoundEnabled: boolean;
  bellNotificationSoundOption: string;
  proVersion: UnknownRecord & {
    freeProjectLimit: number;
    freeBugLimit: number;
    freeFeatureLimit: number;
    enableAiAutomation: boolean;
    allowAiAutomationForFreePlan: boolean;
  };
}

const permissionKeys = [
  "canViewAdmin",
  "canViewTester",
  "canViewDeveloper",
  "canViewAgent",
  "canViewEmployee",
  "canModifyAdmin",
  "canModifyTester",
  "canModifyDeveloper",
  "canModifyAgent",
  "canModifyEmployee"
];

export function SettingsPage() {
  const page = usePage<SettingsModel>(apiRoutes.settings.index);
  const { session } = useSession();
  const [model, setModel] = useState<SettingsModel | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (page.data) setModel(page.data);
  }, [page.data]);

  async function save(endpoint: string) {
    if (!session || !model) return;
    try {
      setError(null);
      const response = await postForm(
        endpoint,
        model,
        session.antiForgeryToken
      );
      setMessage(response.success ?? "Settings saved.");
    } catch (caught) {
      setError(
        caught instanceof ApiError
          ? caught.message
          : "Settings could not be saved."
      );
    }
  }

  return (
    <PageBoundary {...page}>
      {model && (
        <>
          {message && (
            <Notice onDismiss={() => setMessage(null)}>{message}</Notice>
          )}
          {error && (
            <Notice kind="error" onDismiss={() => setError(null)}>
              {error}
            </Notice>
          )}
          <PageHeader
            eyebrow="Administration"
            title="System settings"
            description="Control module access, notifications, plan limits, and automation."
          />
          <section className="surface settings-section">
            <div className="section-heading">
              <div>
                <span className="eyebrow">Permissions</span>
                <h2>Module access by role</h2>
              </div>
              <button
                className="button button-secondary"
                onClick={() =>
                  void save(apiRoutes.settings.saveModulePermissions)}
              >
                Save permissions
              </button>
            </div>
            <div className="permissions-table">
              {model.modules.map((module, moduleIndex) => (
                <article key={module.moduleKey}>
                  <header>
                    <strong>{module.displayName}</strong>
                    <small>{module.moduleKey}</small>
                  </header>
                  {permissionKeys.map((key) => (
                    <label key={key}>
                      <input
                        type="checkbox"
                        checked={Boolean(module[key])}
                        onChange={(event) =>
                          setModel((current) =>
                            current
                              ? {
                                  ...current,
                                  modules: current.modules.map((item, index) =>
                                    index === moduleIndex
                                      ? {
                                          ...item,
                                          [key]: event.target.checked
                                        }
                                      : item)
                                }
                              : current)}
                      />
                      <span>
                        {key
                          .replace(/([a-z])([A-Z])/g, "$1 $2")
                          .replace(/^can /, "")}
                      </span>
                    </label>
                  ))}
                </article>
              ))}
            </div>
          </section>
          <section className="settings-grid">
            <article className="surface settings-section">
              <div>
                <span className="eyebrow">Notifications</span>
                <h2>Bell sound</h2>
              </div>
              <label className="toggle-field">
                <input
                  type="checkbox"
                  checked={model.bellNotificationSoundEnabled}
                  onChange={(event) =>
                    setModel({
                      ...model,
                      bellNotificationSoundEnabled: event.target.checked
                    })}
                />
                <span>Enable notification sound</span>
              </label>
              <label className="form-field">
                <span>Sound</span>
                <select
                  value={model.bellNotificationSoundOption}
                  onChange={(event) =>
                    setModel({
                      ...model,
                      bellNotificationSoundOption: event.target.value
                    })}
                >
                  <option value="classic">Classic</option>
                  <option value="soft">Soft</option>
                  <option value="chime">Chime</option>
                  <option value="alert">Alert</option>
                </select>
              </label>
              <button
                className="button button-secondary"
                onClick={() =>
                  void save(apiRoutes.settings.saveBellNotification)}
              >
                Save sound
              </button>
            </article>
            <article className="surface settings-section">
              <div>
                <span className="eyebrow">Free plan</span>
                <h2>Limits and automation</h2>
              </div>
              {(
                [
                  "freeProjectLimit",
                  "freeBugLimit",
                  "freeFeatureLimit"
                ] as const
              ).map((key) => (
                <label className="form-field" key={key}>
                  <span>
                    {key.replace(/([a-z])([A-Z])/g, "$1 $2")}
                  </span>
                  <input
                    type="number"
                    min={0}
                    max={10000}
                    value={model.proVersion[key]}
                    onChange={(event) =>
                      setModel({
                        ...model,
                        proVersion: {
                          ...model.proVersion,
                          [key]: Number(event.target.value)
                        }
                      })}
                  />
                </label>
              ))}
              <label className="toggle-field">
                <input
                  type="checkbox"
                  checked={model.proVersion.enableAiAutomation}
                  onChange={(event) =>
                    setModel({
                      ...model,
                      proVersion: {
                        ...model.proVersion,
                        enableAiAutomation: event.target.checked
                      }
                    })}
                />
                <span>Enable AI automation</span>
              </label>
              <label className="toggle-field">
                <input
                  type="checkbox"
                  checked={model.proVersion.allowAiAutomationForFreePlan}
                  onChange={(event) =>
                    setModel({
                      ...model,
                      proVersion: {
                        ...model.proVersion,
                        allowAiAutomationForFreePlan: event.target.checked
                      }
                    })}
                />
                <span>Allow AI automation on Free plan</span>
              </label>
              <button
                className="button button-secondary"
                onClick={() => void save(apiRoutes.settings.saveProVersion)}
              >
                Save plan settings
              </button>
            </article>
          </section>
        </>
      )}
    </PageBoundary>
  );
}
