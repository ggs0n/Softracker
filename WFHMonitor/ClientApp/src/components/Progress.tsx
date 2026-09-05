export function Progress({ value }: { value: number }) {
  const percentage = Math.min(100, Math.max(0, value));

  return (
    <span className="progress-cell" aria-label={`${value}% complete`}>
      <span>
        <i style={{ width: `${percentage}%` }} />
      </span>
      <small>{value}%</small>
    </span>
  );
}
