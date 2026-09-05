import { type ReactNode } from "react";
import { AppShell } from "./components/AppShell";
import { ErrorState, LoadingState } from "./components/PageStates";
import { AgentsPage } from "./modules/agents";
import { AccessDeniedPage, AuthPage } from "./modules/auth";
import { BugDetailsPage, BugFormPage, BugsPage } from "./modules/bugs";
import { BrainstormPage } from "./modules/brainstorm";
import { CalendarPage } from "./modules/calendar";
import { DashboardPage } from "./modules/dashboard";
import { DeveloperPage } from "./modules/developer";
import { EmployeesPage } from "./modules/employees";
import {
  FeatureDetailsPage,
  FeatureFormPage,
  FeaturesPage
} from "./modules/features";
import { LegalPage } from "./modules/legal";
import { MonitorPage } from "./modules/monitor";
import { OnboardingPage, OnboardingTeamPage } from "./modules/onboarding";
import { ProfilePage } from "./modules/profile";
import {
  ProjectDetailsPage,
  ProjectFormPage,
  ProjectsPage
} from "./modules/projects";
import { QaDetailsPage, QaPage, TestCaseFormPage } from "./modules/qa";
import { NotFoundPage, WorkspaceHomePage } from "./modules/routing";
import { SettingsPage } from "./modules/settings";
import { SubscriptionPage } from "./modules/subscription";
import { TeamPage } from "./modules/team";
import { Navigate, RouteParams, useLocation } from "./state/RouterContext";
import { useSession } from "./state/SessionContext";

interface RouteMatch {
  element: ReactNode;
  params?: Record<string, string>;
}

function match(pattern: string, pathname: string): Record<string, string> | null {
  const patternParts = pattern.split("/").filter(Boolean);
  const pathParts = pathname.split("/").filter(Boolean);
  if (patternParts.length !== pathParts.length) return null;

  const params: Record<string, string> = {};
  for (let index = 0; index < patternParts.length; index += 1) {
    const patternPart = patternParts[index];
    const pathPart = pathParts[index];
    if (!patternPart || !pathPart) return null;
    if (patternPart.startsWith(":"))
      params[patternPart.slice(1)] = decodeURIComponent(pathPart);
    else if (patternPart !== pathPart)
      return null;
  }
  return params;
}

function resolveRoute(pathname: string): RouteMatch {
  const staticRoutes: Record<string, ReactNode> = {
    "/": <WorkspaceHomePage />,
    "/dashboard": <DashboardPage />,
    "/developer": <DeveloperPage />,
    "/projects": <ProjectsPage />,
    "/projects/new": <ProjectFormPage />,
    "/features": <FeaturesPage />,
    "/features/new": <FeatureFormPage />,
    "/bugs": <BugsPage />,
    "/bugs/new": <BugFormPage />,
    "/brainstorm": <BrainstormPage />,
    "/qa": <QaPage />,
    "/qa/new": <TestCaseFormPage />,
    "/monitor": <MonitorPage />,
    "/calendar": <CalendarPage />,
    "/team": <TeamPage />,
    "/agents": <AgentsPage />,
    "/employees": <EmployeesPage />,
    "/subscription": <SubscriptionPage />,
    "/settings": <SettingsPage />,
    "/profile": <ProfilePage />,
    "/onboarding": <OnboardingPage />,
    "/onboarding/team": <OnboardingTeamPage />,
    "/privacy": <LegalPage kind="privacy" />,
    "/terms": <LegalPage kind="terms" />
  };
  if (staticRoutes[pathname]) return { element: staticRoutes[pathname] };

  const dynamicRoutes: Array<[string, ReactNode]> = [
    ["/projects/:id/edit", <ProjectFormPage />],
    ["/projects/:id", <ProjectDetailsPage />],
    ["/features/:id/edit", <FeatureFormPage />],
    ["/features/:id", <FeatureDetailsPage />],
    ["/bugs/:id/edit", <BugFormPage />],
    ["/bugs/:id", <BugDetailsPage />],
    ["/qa/:id/edit", <TestCaseFormPage />],
    ["/qa/:id", <QaDetailsPage />]
  ];
  for (const [pattern, element] of dynamicRoutes) {
    const params = match(pattern, pathname);
    if (params) return { element, params };
  }
  return { element: <NotFoundPage /> };
}

export default function App() {
  const { session, loading, error, refresh } = useSession();
  const location = useLocation();

  if (loading)
    return <div className="app-boot"><LoadingState rows={3} /></div>;
  if (error && !session)
    return <div className="app-boot"><ErrorState message={error} onRetry={() => void refresh()} /></div>;

  if (location.pathname === "/login")
    return <AuthPage mode="login" />;
  if (location.pathname === "/register")
    return <AuthPage mode="register" />;
  if (location.pathname === "/denied")
    return <AccessDeniedPage />;
  if (!session?.isAuthenticated)
    return (
      <Navigate
        to="/login"
        replace
        state={{ from: location.pathname + location.search }}
      />
    );

  const route = resolveRoute(location.pathname);
  return (
    <AppShell>
      <RouteParams params={route.params ?? {}}>{route.element}</RouteParams>
    </AppShell>
  );
}
