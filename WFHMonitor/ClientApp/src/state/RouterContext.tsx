import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type AnchorHTMLAttributes,
  type MouseEvent,
  type PropsWithChildren,
  type ReactNode
} from "react";

interface LocationState {
  pathname: string;
  search: string;
  state: unknown;
}

interface NavigateOptions {
  replace?: boolean;
  state?: unknown;
}

interface RouterValue {
  basename: string;
  location: LocationState;
  navigate: (to: string, options?: NavigateOptions) => void;
}

const RouterContext = createContext<RouterValue | null>(null);
const ParamsContext = createContext<Record<string, string>>({});

function normalizeBasename(value: string): string {
  const normalized = `/${value}`.replace(/\/+/g, "/").replace(/\/$/, "");
  return normalized === "/" ? "" : normalized;
}

function readLocation(basename: string): LocationState {
  const pathname = window.location.pathname.startsWith(basename)
    ? window.location.pathname.slice(basename.length) || "/"
    : "/";
  return {
    pathname: pathname.startsWith("/") ? pathname : `/${pathname}`,
    search: window.location.search,
    state: (window.history.state as { softrackerState?: unknown } | null)?.softrackerState
  };
}

function safeInternalPath(value: string): string {
  if (!value.startsWith("/") || value.startsWith("//") || value.startsWith("/\\"))
    return "/";
  return value.replace(/\\/g, "/");
}

export function RouterProvider({
  basename: basenameValue,
  children
}: PropsWithChildren<{ basename: string }>) {
  const basename = useMemo(() => normalizeBasename(basenameValue), [basenameValue]);
  const [location, setLocation] = useState(() => readLocation(basename));

  useEffect(() => {
    const update = () => setLocation(readLocation(basename));
    window.addEventListener("popstate", update);
    return () => window.removeEventListener("popstate", update);
  }, [basename]);

  const navigate = useCallback(
    (to: string, options?: NavigateOptions) => {
      const [pathPart = "/", hashPart] = to.split("#", 2);
      const questionIndex = pathPart.indexOf("?");
      const rawPath = questionIndex >= 0 ? pathPart.slice(0, questionIndex) : pathPart;
      const search = questionIndex >= 0 ? pathPart.slice(questionIndex) : "";
      const path = safeInternalPath(rawPath || "/");
      const url = `${basename}${path}${search}${hashPart ? `#${hashPart}` : ""}`;
      const state = { softrackerState: options?.state };
      if (options?.replace)
        window.history.replaceState(state, "", url);
      else
        window.history.pushState(state, "", url);
      setLocation(readLocation(basename));
      window.scrollTo({ top: 0, behavior: "smooth" });
    },
    [basename]
  );

  const value = useMemo(
    () => ({ basename, location, navigate }),
    [basename, location, navigate]
  );
  return <RouterContext.Provider value={value}>{children}</RouterContext.Provider>;
}

function useRouter(): RouterValue {
  const router = useContext(RouterContext);
  if (!router) throw new Error("Router hooks must be used inside RouterProvider.");
  return router;
}

export function useLocation(): LocationState {
  return useRouter().location;
}

export function useNavigate(): RouterValue["navigate"] {
  return useRouter().navigate;
}

export function useParams<T extends Record<string, string | undefined> = Record<string, string>>() {
  return useContext(ParamsContext) as T;
}

export function useSearchParams(): [URLSearchParams] {
  const { search } = useLocation();
  return useMemo(() => [new URLSearchParams(search)], [search]);
}

export function RouteParams({
  params,
  children
}: PropsWithChildren<{ params: Record<string, string> }>) {
  return <ParamsContext.Provider value={params}>{children}</ParamsContext.Provider>;
}

type LinkProps = Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "href"> & {
  to: string;
};

export function Link({ to, onClick, children, ...props }: LinkProps) {
  const { basename, navigate } = useRouter();
  const safePath = safeInternalPath(to);

  function follow(event: MouseEvent<HTMLAnchorElement>) {
    onClick?.(event);
    if (
      event.defaultPrevented ||
      event.button !== 0 ||
      event.metaKey ||
      event.ctrlKey ||
      event.shiftKey ||
      event.altKey
    ) return;
    event.preventDefault();
    navigate(to);
  }

  return (
    <a href={`${basename}${safePath}`} onClick={follow} {...props}>
      {children}
    </a>
  );
}

type NavLinkProps = Omit<LinkProps, "className"> & {
  className?: string | ((state: { isActive: boolean }) => string | undefined);
  children: ReactNode;
};

export function NavLink({ to, className, ...props }: NavLinkProps) {
  const { pathname } = useLocation();
  const target = to.split(/[?#]/, 1)[0] || "/";
  const isActive = pathname === target || (target !== "/" && pathname.startsWith(`${target}/`));
  const resolvedClassName = typeof className === "function"
    ? className({ isActive })
    : className;
  return <Link to={to} className={resolvedClassName} {...props} />;
}

export function Navigate({
  to,
  replace = false,
  state
}: {
  to: string;
  replace?: boolean;
  state?: unknown;
}) {
  const navigate = useNavigate();
  useEffect(() => navigate(to, { replace, state }), [navigate, replace, state, to]);
  return null;
}
