import { useEffect, useId, useState } from "react";
import mermaid from "mermaid";
import type { DesignDiagram } from "../types";
import { downloadDiagramPng } from "../utils/diagrams";
import { copyText } from "../utils/exports";

let mermaidInitialized = false;

function initializeMermaid(): void {
  if (mermaidInitialized) return;
  mermaid.initialize({
    startOnLoad: false,
    securityLevel: "strict",
    theme: "base",
    themeVariables: {
      primaryColor: "#f7f4ed",
      primaryTextColor: "#17231f",
      primaryBorderColor: "#718078",
      lineColor: "#33453e",
      secondaryColor: "#e8f3ef",
      tertiaryColor: "#fff0e9",
      fontFamily: "Inter, Segoe UI, Arial, sans-serif"
    }
  });
  mermaidInitialized = true;
}

interface DiagramPanelProps {
  diagram: DesignDiagram;
}

export function DiagramPanel({ diagram }: DiagramPanelProps) {
  const [svg, setSvg] = useState("");
  const [renderError, setRenderError] = useState<string | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const reactId = useId();
  const diagramId = `brainstorm-${diagram.kind}-${reactId.replace(/:/g, "")}`;
  const isWide =
    diagram.kind === "detailedArchitecture"
    || diagram.kind === "cloudDeploymentTemplate";

  useEffect(() => {
    let cancelled = false;
    initializeMermaid();

    async function render(): Promise<void> {
      try {
        const result = await mermaid.render(diagramId, diagram.mermaid);
        if (!cancelled) {
          setSvg(result.svg);
          setRenderError(null);
        }
      } catch (caught) {
        if (!cancelled) {
          setSvg("");
          setRenderError(
            caught instanceof Error
              ? caught.message
              : "The diagram could not be rendered."
          );
        }
      }
    }

    void render();
    return () => {
      cancelled = true;
    };
  }, [diagram.mermaid, diagramId]);

  async function savePng(): Promise<void> {
    if (svg) await downloadDiagramPng(svg, diagram.title);
  }

  const source = diagram.mermaid;
  return (
    <section className={`bsm-diagram-panel${isWide ? " bsm-diagram-wide" : ""}`}>
      <header>
        <div>
          <span>{diagram.kind.replace(/([A-Z])/g, " $1")}</span>
          <h3>{diagram.title}</h3>
        </div>
        <div className="bsm-diagram-actions">
          <button
            className="bsm-icon-button"
            type="button"
            title="Copy Mermaid source"
            aria-label={`Copy ${diagram.title} source`}
            disabled={!source}
            onClick={() => void copyText(source)}
          >
            <i className="bi bi-copy" />
          </button>
          <button
            className="bsm-icon-button"
            type="button"
            title="Open fullscreen"
            aria-label={`Open ${diagram.title} fullscreen`}
            disabled={!svg}
            onClick={() => setIsFullscreen(true)}
          >
            <i className="bi bi-arrows-fullscreen" />
          </button>
          <button
            className="bsm-icon-button"
            type="button"
            title="Save as PNG"
            aria-label={`Save ${diagram.title} as PNG`}
            disabled={!svg}
            onClick={() => void savePng()}
          >
            <i className="bi bi-download" />
          </button>
        </div>
      </header>
      <div className="bsm-diagram-canvas">
        {renderError ? (
          <pre className="bsm-diagram-error">{renderError}</pre>
        ) : (
          <div dangerouslySetInnerHTML={{ __html: svg }} />
        )}
      </div>
      <details className="bsm-source-block">
        <summary>Mermaid source</summary>
        <pre>{source}</pre>
      </details>

      {isFullscreen && (
        <div
          className="bsm-fullscreen"
          role="dialog"
          aria-modal="true"
          aria-label={`${diagram.title} fullscreen`}
        >
          <header>
            <h3>{diagram.title}</h3>
            <div className="bsm-diagram-actions">
              <button
                className="bsm-icon-button"
                type="button"
                aria-label="Save as PNG"
                onClick={() => void savePng()}
              >
                <i className="bi bi-download" />
              </button>
              <button
                className="bsm-icon-button"
                type="button"
                aria-label="Close fullscreen"
                onClick={() => setIsFullscreen(false)}
              >
                <i className="bi bi-x-lg" />
              </button>
            </div>
          </header>
          <div
            className="bsm-fullscreen-canvas"
            dangerouslySetInnerHTML={{ __html: svg }}
          />
        </div>
      )}
    </section>
  );
}
