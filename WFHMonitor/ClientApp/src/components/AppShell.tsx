import { useEffect, useMemo, useState, type PropsWithChildren } from "react";
import brandImageUrl from "../assets/cat1.jpg";
import { apiRoutes, postForm } from "../services/microservices";
import { Link, NavLink, useLocation, useNavigate } from "../state/RouterContext";
import { useSession } from "../state/SessionContext";

interface NavigationItem {
  label: string;
  to: string;
  icon: string;
  visible: boolean;
}

function LocalClock() {
  const [now, setNow] = useState(new Date());
  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  return (
    <time className="local-clock" dateTime={now.toISOString()}>
      <strong>
        {now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
      </strong>
      <span>{now.toLocaleDateString([], { weekday: "short", day: "numeric", month: "short" })}</span>
    </time>
  );
}

export function AppShell({ children }: PropsWithChildren) {
  const { session, refresh } = useSession();
  const location = useLocation();
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const user = session?.user;
  const roles = session?.roles ?? [];
  const access = session?.access;

  useEffect(() => {
    setMenuOpen(false);
    setNotificationsOpen(false);
  }, [location.pathname]);

  const navigation = useMemo<NavigationItem[]>(
    () => [
      {
        label: "Dashboard",
        to: "/dashboard",
        icon: "bi-grid-1x2",
        visible: roles.includes("Admin")
      },
      {
        label: "Developer summary",
        to: "/developer",
        icon: "bi-clipboard-data",
        visible: roles.includes("Developer")
      },
      {
        label: "Projects",
        to: "/projects",
        icon: "bi-kanban",
        visible: access?.canViewAllProjects ?? false
      },
      {
        label: "Features",
        to: "/features",
        icon: "bi-list-check",
        visible: access?.canViewFeatures ?? false
      },
      {
        label: "Bugs",
        to: "/bugs",
        icon: "bi-bug",
        visible: access?.canViewBugs ?? false
      },
      {
        label: "QA testing",
        to: "/qa",
        icon: "bi-shield-check",
        visible: access?.canViewQaTesting ?? false
      },
      {
        label: "Monitor",
        to: "/monitor",
        icon: "bi-activity",
        visible: roles.some((role) => ["Admin", "Tester", "Developer"].includes(role))
      },
      {
        label: "Calendar",
        to: "/calendar",
        icon: "bi-calendar3",
        visible: true
      },
      {
        label: "Brainstorm",
        to: "/brainstorm",
        icon: "bi-lightbulb",
        visible: true
      },
      {
        label: "Team",
        to: "/team",
        icon: "bi-diagram-3",
        visible: roles.includes("Admin")
      },
      {
        label: "Agents",
        to: "/agents",
        icon: "bi-cpu",
        visible: roles.some((role) => ["Admin", "Tester", "Developer", "Agent"].includes(role))
      },
      {
        label: "Employees",
        to: "/employees",
        icon: "bi-person-badge",
        visible: roles.includes("Admin")
      },
      {
        label: "Subscription",
        to: "/subscription",
        icon: "bi-credit-card-2-front",
        visible: true
      },
      {
        label: "Settings",
        to: "/settings",
        icon: "bi-sliders",
        visible: roles.includes("Admin")
      }
    ],
    [access, roles]
  );

  async function logout() {
    if (!session) return;
    await postForm(apiRoutes.auth.logout, {}, session.antiForgeryToken);
    await refresh();
    navigate("/login", { replace: true });
  }

  async function markAllRead() {
    if (!session) return;
    await postForm(
      apiRoutes.notifications.markAllRead,
      {},
      session.antiForgeryToken
    );
    await refresh();
  }

  async function disconnectGitHub() {
    if (!session) return;
    await postForm(
      apiRoutes.github.disconnect,
      { returnUrl: "/app/" },
      session.antiForgeryToken
    );
    await refresh();
  }

  async function openNotification(id: number, linkUrl?: string | null) {
    if (!session) return;
    await postForm(
      apiRoutes.notifications.markRead,
      { id, returnUrl: "/app/" },
      session.antiForgeryToken
    );
    await refresh();
    if (linkUrl) window.location.assign(linkUrl);
  }

  const initials = user?.name
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase() ?? "ST";

  return (
    <div className="app-frame">
      <button
        className="mobile-menu-button"
        type="button"
        aria-label="Toggle navigation"
        aria-expanded={menuOpen}
        onClick={() => setMenuOpen((open) => !open)}
      >
        <i className="bi bi-list" aria-hidden="true" />
      </button>
      {menuOpen && <button className="nav-scrim" aria-label="Close navigation" onClick={() => setMenuOpen(false)} />}
      <aside className={`app-nav ${menuOpen ? "app-nav-open" : ""}`}>
        <Link className="brand" to="/">
          <img src={brandImageUrl} alt="" />
          <span>Softracker</span>
        </Link>
        <span className="nav-label">Workspace</span>
        <nav aria-label="Primary navigation">
          {navigation.filter((item) => item.visible).map((item) => (
            <NavLink
              className={({ isActive }) => (isActive ? "active" : undefined)}
              key={item.to}
              to={item.to}
            >
              <i className={`bi ${item.icon}`} aria-hidden="true" />
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>
        <div className="nav-profile">
          <span className="avatar">
            {user?.photoUrl ? <img src={user.photoUrl} alt={`${user.name} profile`} /> : initials}
          </span>
          <Link to="/profile">
            <strong>{user?.name}</strong>
            <small>{roles[0] ?? "Employee"} · {user?.hasProAccess ? "Pro" : "Free"}</small>
          </Link>
          <button type="button" aria-label="Sign out" onClick={() => void logout()}>
            <i className="bi bi-box-arrow-right" aria-hidden="true" />
          </button>
        </div>
      </aside>

      <div className="app-body">
        <header className="app-topbar">
          <LocalClock />
          <div className="topbar-actions">
            {session?.isGitHubOAuthConfigured && (
              session.isGitHubConnected ? (
                <button className="github-state" type="button" title="Disconnect GitHub" onClick={() => void disconnectGitHub()}><i className="bi bi-github" /> Connected</button>
              ) : (
                <a className="icon-text-button" href="/GitHubAuth/Connect?returnUrl=/app/">
                  <i className="bi bi-github" /> Connect
                </a>
              )
            )}
            <div className="notification-menu">
              <button
                className="icon-button"
                type="button"
                aria-label="Notifications"
                aria-expanded={notificationsOpen}
                onClick={() => setNotificationsOpen((open) => !open)}
              >
                <i className="bi bi-bell" aria-hidden="true" />
                {(session?.notifications.unreadCount ?? 0) > 0 && (
                  <span>{session?.notifications.unreadCount}</span>
                )}
              </button>
              {notificationsOpen && (
                <section className="notification-panel">
                  <header>
                    <strong>Notifications</strong>
                    <button type="button" onClick={() => void markAllRead()}>Mark all read</button>
                  </header>
                  <div>
                    {(session?.notifications.items ?? []).length === 0 ? (
                      <p>You’re all caught up.</p>
                    ) : (
                      session?.notifications.items.map((item) => (
                        <article className={item.isRead ? "" : "unread"} key={item.id}>
                          <button type="button" onClick={() => void openNotification(item.id, item.linkUrl)}>
                            <strong>{item.title}</strong>
                            <p>{item.message}</p>
                            <time dateTime={item.createdAt}>
                              {new Date(item.createdAt).toLocaleDateString()}
                            </time>
                          </button>
                        </article>
                      ))
                    )}
                  </div>
                </section>
              )}
            </div>
          </div>
        </header>
        <main id="main-content" className="page-container">
          {children}
        </main>
        <footer className="app-footer">
          <span>© {new Date().getFullYear()} Softracker</span>
          <Link to="/privacy">Privacy</Link>
          <Link to="/terms">Terms</Link>
        </footer>
      </div>
    </div>
  );
}
