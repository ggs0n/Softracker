import { useEffect, useMemo, useState } from "react";
import { useSession } from "../../../state/SessionContext";
import { generateModuleImages } from "../api";
import type {
  DesignProjectDetails,
  GeneratedModuleImage
} from "../types";
import { downloadDiagramPng } from "../utils/diagrams";
import { copyText } from "../utils/exports";
import {
  buildModuleArchitectureImages,
  MAX_MODULE_IMAGES,
  type ModuleArchitectureImage
} from "../utils/moduleImages";

interface ModuleImagesPanelProps {
  design: DesignProjectDetails;
}

export function ModuleImagesPanel({ design }: ModuleImagesPanelProps) {
  const { session } = useSession();
  const images = useMemo(
    () => buildModuleArchitectureImages(design),
    [design]
  );
  const [fullscreenImage, setFullscreenImage] =
    useState<ModuleArchitectureImage | null>(null);
  const [generatedImages, setGeneratedImages] =
    useState<GeneratedModuleImage[]>([]);
  const [isGenerating, setIsGenerating] = useState(false);
  const [generationError, setGenerationError] =
    useState<string | null>(null);
  const isAdmin = session?.roles.includes("Admin") ?? false;

  useEffect(() => {
    setGeneratedImages([]);
    setGenerationError(null);
    setFullscreenImage(null);
  }, [design.id]);

  async function handleGenerate(): Promise<void> {
    if (!session || !isAdmin || isGenerating) return;
    setIsGenerating(true);
    setGenerationError(null);
    try {
      const result = await generateModuleImages(
        design.id,
        session.antiForgeryToken
      );
      if (!result.succeeded)
        throw new Error(result.error || "No module image was generated.");
      setGeneratedImages(result.images.slice(0, MAX_MODULE_IMAGES));
    } catch (caught) {
      setGenerationError(
        caught instanceof Error
          ? caught.message
          : "Module images could not be generated."
      );
    } finally {
      setIsGenerating(false);
    }
  }

  if (images.length === 0) {
    return (
      <div className="bsm-info-message">
        <i className="bi bi-images" />
        <p>
          No main module is available. Add a main feature or domain module,
          then generate the blueprint again.
        </p>
      </div>
    );
  }

  return (
    <section className="bsm-module-images-panel">
      <header className="bsm-module-images-heading">
        <div>
          <span className="bsm-kicker">Main-module visual set</span>
          <h3>Generated module images</h3>
          <p>
            Preview each module’s exact scope, then generate the related
            image set through the connected ChatGPT OAuth account.
          </p>
        </div>
        <div className="bsm-module-generate-actions">
          <strong>{images.length} / {MAX_MODULE_IMAGES} images</strong>
          <button
            className="button button-primary"
            type="button"
            disabled={!isAdmin || isGenerating}
            onClick={() => void handleGenerate()}
          >
            <i
              className={`bi ${
                isGenerating
                  ? "bi-arrow-repeat bsm-spin"
                  : "bi-stars"
              }`}
            />
            {isGenerating
              ? "Generating images…"
              : generatedImages.length > 0
                ? "Regenerate with ChatGPT"
                : "Generate with ChatGPT"}
          </button>
        </div>
      </header>

      {!isAdmin && (
        <div className="bsm-info-message">
          <i className="bi bi-person-lock" />
          <p>Only administrators can generate Codex-backed images.</p>
        </div>
      )}

      {generationError && (
        <div className="bsm-alert" role="alert">
          <i className="bi bi-exclamation-circle" />
          <span>{generationError}</span>
        </div>
      )}

      {generatedImages.length > 0 ? (
        <div className="bsm-module-images-grid">
          {generatedImages.map((image) => (
            <GeneratedImageCard
              image={image}
              key={`${image.moduleName}-${image.base64Data.slice(0, 24)}`}
            />
          ))}
        </div>
      ) : (
        <>
          <div className="bsm-module-plan-label">
            <div>
              <span className="bsm-kicker">Generation scope</span>
              <h4>Main-module image previews</h4>
            </div>
            <small>
              Infrastructure unrelated to a module is excluded.
            </small>
          </div>
          <div className="bsm-module-images-grid">
            {images.map((image) => (
              <ModuleImageCard
                image={image}
                key={image.moduleName}
                onFullscreen={() => setFullscreenImage(image)}
              />
            ))}
          </div>
        </>
      )}

      {fullscreenImage && (
        <div
          className="bsm-fullscreen"
          role="dialog"
          aria-modal="true"
          aria-label={`${fullscreenImage.moduleName} module image fullscreen`}
        >
          <header>
            <h3>{fullscreenImage.moduleName} module image</h3>
            <div className="bsm-diagram-actions">
              <button
                className="bsm-icon-button"
                type="button"
                aria-label="Save module image as PNG"
                onClick={() =>
                  void downloadDiagramPng(
                    fullscreenImage.svg,
                    `${fullscreenImage.moduleName} Module`
                  )
                }
              >
                <i className="bi bi-download" />
              </button>
              <button
                className="bsm-icon-button"
                type="button"
                aria-label="Close fullscreen"
                onClick={() => setFullscreenImage(null)}
              >
                <i className="bi bi-x-lg" />
              </button>
            </div>
          </header>
          <div
            className="bsm-fullscreen-canvas bsm-module-fullscreen-canvas"
            dangerouslySetInnerHTML={{ __html: fullscreenImage.svg }}
          />
        </div>
      )}
    </section>
  );
}

function GeneratedImageCard({
  image
}: {
  image: GeneratedModuleImage;
}) {
  const source = `data:${image.mimeType};base64,${image.base64Data}`;
  return (
    <article className="bsm-module-image-card bsm-generated-image-card">
      <header>
        <div>
          <span>ChatGPT OAuth image</span>
          <h4>{image.moduleName}</h4>
        </div>
        <button
          className="bsm-icon-button"
          type="button"
          title="Save image"
          aria-label={`Save ${image.moduleName} generated image`}
          onClick={() => downloadGeneratedImage(image, source)}
        >
          <i className="bi bi-download" />
        </button>
      </header>
      <div className="bsm-generated-image-canvas">
        <img
          src={source}
          alt={`Technical illustration focused on the ${image.moduleName} main module`}
        />
      </div>
      {image.revisedPrompt && (
        <details className="bsm-source-block">
          <summary>Image prompt used by ChatGPT</summary>
          <p>{image.revisedPrompt}</p>
        </details>
      )}
    </article>
  );
}

function downloadGeneratedImage(
  image: GeneratedModuleImage,
  source: string
): void {
  const extension = image.mimeType === "image/jpeg"
    ? "jpg"
    : image.mimeType.split("/")[1];
  const name = image.moduleName
    .toLocaleLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
  const anchor = document.createElement("a");
  anchor.href = source;
  anchor.download = `${name || "module"}-image.${extension}`;
  anchor.click();
}

function ModuleImageCard({
  image,
  onFullscreen
}: {
  image: ModuleArchitectureImage;
  onFullscreen: () => void;
}) {
  return (
    <article className="bsm-module-image-card">
      <header>
        <div>
          <span>Main module</span>
          <h4>{image.moduleName}</h4>
        </div>
        <div className="bsm-diagram-actions">
          <button
            className="bsm-icon-button"
            type="button"
            title="Copy SVG"
            aria-label={`Copy ${image.moduleName} module SVG`}
            onClick={() => void copyText(image.svg)}
          >
            <i className="bi bi-copy" />
          </button>
          <button
            className="bsm-icon-button"
            type="button"
            title="Open fullscreen"
            aria-label={`Open ${image.moduleName} image fullscreen`}
            onClick={onFullscreen}
          >
            <i className="bi bi-arrows-fullscreen" />
          </button>
          <button
            className="bsm-icon-button"
            type="button"
            title="Save as PNG"
            aria-label={`Save ${image.moduleName} module image as PNG`}
            onClick={() =>
              void downloadDiagramPng(
                image.svg,
                `${image.moduleName} Module`
              )
            }
          >
            <i className="bi bi-download" />
          </button>
        </div>
      </header>
      <div
        className="bsm-module-image-canvas"
        dangerouslySetInnerHTML={{ __html: image.svg }}
      />
      <footer>
        <span><strong>{image.restEndpointCount}</strong> REST</span>
        <span><strong>{image.grpcContractCount}</strong> gRPC</span>
        <span><strong>{image.tableCount}</strong> tables</span>
        <span><strong>{image.batchJobCount}</strong> jobs</span>
      </footer>
    </article>
  );
}
