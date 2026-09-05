import type { ReactNode } from "react";
import { ErrorState, LoadingState } from "./PageStates";

interface PageBoundaryProps {
  loading: boolean;
  error: string | null;
  reload: () => void;
  children: ReactNode;
}

export function PageBoundary({
  loading,
  error,
  reload,
  children
}: PageBoundaryProps) {
  if (loading) return <LoadingState />;
  if (error) return <ErrorState message={error} onRetry={reload} />;
  return children;
}
