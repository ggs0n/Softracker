import { Progress } from "../../../components/Progress";

interface CoverageCardProps {
  module: string;
  total: number;
  passed: number;
  coveragePercent: number;
}

export function CoverageCard({
  module,
  total,
  passed,
  coveragePercent
}: CoverageCardProps) {
  return (
    <article className="surface">
      <header>
        <strong>{module}</strong>
        <span>
          {passed}/{total}
        </span>
      </header>
      <Progress value={coveragePercent} />
    </article>
  );
}
