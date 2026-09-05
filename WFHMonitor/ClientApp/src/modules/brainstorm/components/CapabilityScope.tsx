export function CapabilityScope({ features }: { features: string }) {
  const capabilities = extractCapabilities(features);
  if (capabilities.length === 0) return null;

  return (
    <section className="bsm-capability-scope" aria-label="Canonical capability scope">
      <div>
        <span className="bsm-kicker">Canonical blueprint scope</span>
        <h3>Capabilities used across every tab</h3>
      </div>
      <div className="bsm-capability-tags">
        {capabilities.map((capability) => (
          <span key={capability}>
            <i className="bi bi-check-circle-fill" aria-hidden="true" />
            {capability}
          </span>
        ))}
      </div>
    </section>
  );
}

function extractCapabilities(features: string): string[] {
  const seen = new Set<string>();
  return features
    .split(/[,;\n\r]+/)
    .map((value) => value.replace(/\s+/g, " ").trim())
    .filter((value) => {
      if (!value) return false;
      const key = value.toLocaleLowerCase();
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    })
    .slice(0, 10)
    .map((value) => value.charAt(0).toLocaleUpperCase() + value.slice(1));
}
