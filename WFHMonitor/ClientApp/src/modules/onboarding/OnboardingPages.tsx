import { useState, type FormEvent } from "react";
import { PageHeader, StatusChip } from "../../components/DataDisplay";
import { PageBoundary } from "../../components/PageBoundary";
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
import { Link, useNavigate } from "../../state/RouterContext";
import { useSession } from "../../state/SessionContext";

interface OnboardingWelcomeModel {
  canUseGitHubOAuth: boolean;
  isGitHubConnected: boolean;
  connectUrl: string;
  returnUrl: string;
  noAccessMessage?: string | null;
}

interface SetupMember {
  email: string;
  role: string;
}

interface SetupTeamModel {
  returnUrl: string;
  members: SetupMember[];
}

export function OnboardingPage() {
  const page = usePage<OnboardingWelcomeModel>(
    apiRoutes.onboarding.welcome("/app/")
  );
  const navigate = useNavigate();

  return (
    <PageBoundary {...page}>
      {page.data ? (
        <>
          <PageHeader
            eyebrow="Workspace setup"
            title="Connect your engineering flow"
            description="A repository connection lets Softracker detect scope and power project automation."
          />
          <section className="surface onboarding-panel">
            <i className="bi bi-github" aria-hidden="true" />
            <div>
              <h2>GitHub integration</h2>
              <p>
                {page.data.noAccessMessage
                  ?? "Connect a GitHub account to import repositories and enrich delivery tracking."}
              </p>
            </div>
            {page.data.canUseGitHubOAuth
            && !page.data.isGitHubConnected ? (
              <a className="button button-primary" href={page.data.connectUrl}>
                Connect GitHub
              </a>
            ) : (
              <StatusChip
                value={page.data.isGitHubConnected
                  ? "Connected"
                  : "Restricted"}
              />
            )}
            <ServerAction
              endpoint={apiRoutes.onboarding.skipWelcome}
              values={{ returnUrl: "/app/" }}
              onComplete={() => navigate("/onboarding/team")}
            >
              Skip for now
            </ServerAction>
          </section>
        </>
      ) : (
        <EmptyState
          description="Workspace setup is already complete."
          action={
            <Link className="button button-primary" to="/">
              Open workspace
            </Link>
          }
        />
      )}
    </PageBoundary>
  );
}

export function OnboardingTeamPage() {
  const page = usePage<SetupTeamModel>(
    apiRoutes.onboarding.setupTeam("/app/")
  );
  const { session } = useSession();
  const navigate = useNavigate();
  const [members, setMembers] = useState<SetupMember[]>([
    { email: "", role: "Employee" }
  ]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function update(index: number, values: Partial<SetupMember>) {
    setMembers((current) =>
      current.map((member, memberIndex) =>
        memberIndex === index ? { ...member, ...values } : member)
    );
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!session) return;
    try {
      setBusy(true);
      setError(null);
      await postForm(
        apiRoutes.onboarding.setupTeam(),
        {
          returnUrl: "/app/",
          memberCount: members.length,
          members
        },
        session.antiForgeryToken
      );
      navigate("/");
    } catch (caught) {
      setError(
        caught instanceof ApiError
          ? caught.message
          : "Team setup could not be completed."
      );
    } finally {
      setBusy(false);
    }
  }

  if (page.loading) return <LoadingState />;
  if (page.error)
    return <ErrorState message={page.error} onRetry={page.reload} />;
  if (!page.data)
    return <EmptyState description="Team setup is not available." />;

  return (
    <>
      <PageHeader
        eyebrow="Workspace setup"
        title="Invite your first team"
        description="Add delivery roles now, or continue and manage employees later."
      />
      <form
        className="surface entity-form setup-team-form"
        onSubmit={(event) => void submit(event)}
      >
        {error && <Notice kind="error">{error}</Notice>}
        <div className="setup-member-list">
          {members.map((member, index) => (
            <div key={index}>
              <label className="form-field">
                <span>Email</span>
                <input
                  type="email"
                  required
                  value={member.email}
                  onChange={(event) =>
                    update(index, { email: event.target.value })}
                />
              </label>
              <label className="form-field">
                <span>Role</span>
                <select
                  value={member.role}
                  onChange={(event) =>
                    update(index, { role: event.target.value })}
                >
                  <option>Employee</option>
                  <option>Developer</option>
                  <option>Tester</option>
                  <option>Agent</option>
                </select>
              </label>
              {members.length > 1 && (
                <button
                  className="icon-button"
                  type="button"
                  aria-label={`Remove member ${index + 1}`}
                  onClick={() =>
                    setMembers((current) =>
                      current.filter(
                        (_, memberIndex) => memberIndex !== index
                      ))}
                >
                  <i className="bi bi-x-lg" />
                </button>
              )}
            </div>
          ))}
        </div>
        <button
          className="text-button"
          type="button"
          onClick={() =>
            setMembers((current) => [
              ...current,
              { email: "", role: "Employee" }
            ])}
        >
          <i className="bi bi-plus-lg" /> Add another member
        </button>
        <footer className="form-footer">
          <ServerAction
            endpoint={apiRoutes.onboarding.skipSetupTeam}
            values={{ returnUrl: "/app/" }}
            onComplete={() => navigate("/")}
          >
            Skip
          </ServerAction>
          <button
            className="button button-primary"
            disabled={busy}
            type="submit"
          >
            Finish setup
          </button>
        </footer>
      </form>
      <p className="form-footnote">
        Invited accounts receive the current onboarding default password. Ask
        each person to change it after first sign-in.
      </p>
      <Link className="text-button" to="/employees">
        Manage existing employees
      </Link>
    </>
  );
}
