import { Navigate } from "../../state/RouterContext";
import { useSession } from "../../state/SessionContext";

export function WorkspaceHomePage() {
  const { session } = useSession();

  return (
    <Navigate
      replace
      to={
        session?.roles.includes("Admin")
          ? "/dashboard"
          : session?.roles.includes("Developer")
            ? "/developer"
            : "/projects"
      }
    />
  );
}

export function NotFoundPage() {
  return (
    <section className="standalone-state">
      <i className="bi bi-signpost-split" aria-hidden="true" />
      <h1>Page not found</h1>
      <p>The destination may have moved or your role may not include it.</p>
      <a className="button button-primary" href="/app/">
        Return to workspace
      </a>
    </section>
  );
}
