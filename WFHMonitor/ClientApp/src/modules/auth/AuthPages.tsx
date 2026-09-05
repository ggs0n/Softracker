import { useState, type FormEvent } from "react";
import brandImageUrl from "../../assets/cat1.jpg";
import {
  ApiError,
  apiRoutes,
  postForm
} from "../../services/microservices";
import { Notice } from "../../components/PageStates";
import {
  Link,
  Navigate,
  useLocation,
  useNavigate
} from "../../state/RouterContext";
import { useSession } from "../../state/SessionContext";

type AuthMode = "login" | "register";

export function AuthPage({ mode }: { mode: AuthMode }) {
  const { session, refresh } = useSession();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [companyName, setCompanyName] = useState("");
  const [rememberMe, setRememberMe] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  if (session?.isAuthenticated) return <Navigate to="/" replace />;

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!session) return;

    try {
      setBusy(true);
      setError(null);
      setFieldErrors({});
      const endpoint =
        mode === "login" ? apiRoutes.auth.login : apiRoutes.auth.register;
      const values = mode === "login"
        ? { email, password, rememberMe, returnUrl: "/" }
        : { email, password, confirmPassword, companyName, returnUrl: "/" };
      await postForm(endpoint, values, session.antiForgeryToken);
      const nextSession = await refresh();
      if (nextSession?.isAuthenticated) {
        const requestedPath = (location.state as { from?: string } | null)?.from;
        navigate(requestedPath ?? "/", { replace: true });
      } else {
        setError("The account was created, but the session could not be started.");
      }
    } catch (caught) {
      if (caught instanceof ApiError) {
        setError(caught.message);
        setFieldErrors(caught.errors);
      } else {
        setError("Authentication failed. Please try again.");
      }
    } finally {
      setBusy(false);
    }
  }

  const getError = (name: string) =>
    fieldErrors[name]?.[0] ?? fieldErrors[name[0]?.toUpperCase() + name.slice(1)]?.[0];

  return (
    <main className="auth-page" id="main-content">
      <section className="auth-story" aria-label="About Softracker">
        <Link className="auth-brand" to="/">
          <img src={brandImageUrl} alt="" />
          <span>Softracker</span>
        </Link>
        <div>
          <span className="eyebrow">Delivery operations</span>
          <h1>Keep the work visible. Ship with fewer surprises.</h1>
          <p>
            Projects, quality checks, team activity, and engineering automation live in one focused workspace.
          </p>
        </div>
        <small>Built for distributed software teams.</small>
      </section>
      <section className="auth-form-panel">
        <form className="auth-form" onSubmit={(event) => void submit(event)}>
          <header>
            <span className="eyebrow">{mode === "login" ? "Welcome back" : "Create workspace"}</span>
            <h2>{mode === "login" ? "Sign in to Softracker" : "Start your account"}</h2>
            <p>
              {mode === "login"
                ? "Use the email connected to your workspace."
                : "You can add teammates after setup."}
            </p>
          </header>
          {error && <Notice kind="error">{error}</Notice>}
          <label>
            <span>Email address</span>
            <input
              autoComplete="email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
            {getError("email") && <small className="field-error">{getError("email")}</small>}
          </label>
          <label>
            <span>Password</span>
            <input
              autoComplete={mode === "login" ? "current-password" : "new-password"}
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
            {getError("password") && <small className="field-error">{getError("password")}</small>}
          </label>
          {mode === "register" && (
            <>
              <label>
                <span>Confirm password</span>
                <input
                  autoComplete="new-password"
                  type="password"
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                  required
                />
                {getError("confirmPassword") && (
                  <small className="field-error">{getError("confirmPassword")}</small>
                )}
              </label>
              <label>
                <span>Company name <em>optional</em></span>
                <input
                  autoComplete="organization"
                  value={companyName}
                  onChange={(event) => setCompanyName(event.target.value)}
                  maxLength={200}
                />
              </label>
            </>
          )}
          {mode === "login" && (
            <label className="check-field">
              <input
                type="checkbox"
                checked={rememberMe}
                onChange={(event) => setRememberMe(event.target.checked)}
              />
              <span>Keep me signed in</span>
            </label>
          )}
          <button className="button button-primary button-wide" type="submit" disabled={busy}>
            {busy && <i className="bi bi-arrow-repeat spin" aria-hidden="true" />}
            {mode === "login" ? "Sign in" : "Create account"}
          </button>
          <p className="auth-switch">
            {mode === "login" ? "New to Softracker?" : "Already have an account?"}{" "}
            <Link to={mode === "login" ? "/register" : "/login"}>
              {mode === "login" ? "Create an account" : "Sign in"}
            </Link>
          </p>
        </form>
      </section>
    </main>
  );
}

export function AccessDeniedPage() {
  return (
    <main className="standalone-state" id="main-content">
      <i className="bi bi-sign-stop" aria-hidden="true" />
      <h1>Access restricted</h1>
      <p>Your current role does not include this workspace area.</p>
      <Link className="button button-primary" to="/">Return to workspace</Link>
    </main>
  );
}
