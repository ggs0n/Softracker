import type {
  CostEstimate,
  DeploymentService,
  DesignBlueprint,
  DesignProjectDetails,
  ResultTab
} from "../types";
import {
  copyText,
  deploymentServicesToMarkdown,
  downloadMarkdown,
  toMarkdown
} from "../utils/exports";
import { CapabilityScope } from "./CapabilityScope";
import { DiagramPanel } from "./DiagramPanel";
import { ModuleImagesPanel } from "./ModuleImagesPanel";
import { SchemaPanel } from "./SchemaPanel";
import { ServicesApiPanel } from "./ServicesApiPanel";

interface BlueprintWorkspaceProps {
  design: DesignProjectDetails | null;
  activeTab: ResultTab;
  onTabChange: (tab: ResultTab) => void;
}

const tabs: Array<{ id: ResultTab; label: string; icon: string }> = [
  { id: "overview", label: "Overview", icon: "bi-file-text" },
  { id: "architecture", label: "Architecture", icon: "bi-boxes" },
  { id: "services", label: "Services & APIs", icon: "bi-hdd-network" },
  { id: "schema", label: "Schema", icon: "bi-table" },
  { id: "risks", label: "Risks", icon: "bi-shield-check" },
  { id: "diagrams", label: "Diagrams", icon: "bi-diagram-3" },
  { id: "moduleImages", label: "Module Images", icon: "bi-images" }
];

export function BlueprintWorkspace({
  design,
  activeTab,
  onTabChange
}: BlueprintWorkspaceProps) {
  if (!design) return <BlueprintEmptyState />;
  const blueprint = design.blueprint;

  return (
    <section className="bsm-card bsm-blueprint-card" aria-label="Generated blueprint">
      <header className="bsm-blueprint-heading">
        <div>
          <span className="bsm-source-badge">
            <i className="bi bi-stars" />
            {blueprint.sourceMode}
          </span>
          <h2>{blueprint.title}</h2>
          <p>{design.summary}</p>
        </div>
        <div className="bsm-blueprint-actions">
          <button
            className="button button-secondary"
            type="button"
            onClick={() => void copyText(toMarkdown(design))}
          >
            <i className="bi bi-copy" /> Copy
          </button>
          <button
            className="button button-secondary"
            type="button"
            onClick={() => downloadMarkdown(design)}
          >
            <i className="bi bi-download" /> Markdown
          </button>
        </div>
      </header>

      {blueprint.notice && (
        <div className="bsm-notice">
          <i className="bi bi-info-circle" />
          <span>{blueprint.notice}</span>
        </div>
      )}

      <nav className="bsm-tabs" aria-label="Blueprint sections">
        {tabs.map((tab) => (
          <button
            type="button"
            className={activeTab === tab.id ? "active" : ""}
            aria-current={activeTab === tab.id ? "page" : undefined}
            key={tab.id}
            onClick={() => onTabChange(tab.id)}
          >
            <i className={`bi ${tab.icon}`} />
            {tab.label}
          </button>
        ))}
      </nav>

      <div className="bsm-tab-content">
        <CapabilityScope features={design.features} />
        {activeTab === "overview" && <OverviewTab blueprint={blueprint} />}
        {activeTab === "architecture" && (
          <ArchitectureTab blueprint={blueprint} />
        )}
        {activeTab === "services" && (
          <ServicesApiPanel
            services={blueprint.serviceDetails ?? []}
            restEndpoints={blueprint.restEndpoints ?? []}
            grpcContracts={blueprint.grpcContracts ?? []}
            batchJobs={blueprint.batchJobs ?? []}
          />
        )}
        {activeTab === "schema" && (
          <SchemaPanel schemas={blueprint.tableSchemas ?? []} />
        )}
        {activeTab === "risks" && <RisksTab blueprint={blueprint} />}
        {activeTab === "diagrams" && (
          <div className="bsm-diagrams-grid">
            {blueprint.diagrams.map((diagram) => (
              <DiagramPanel
                diagram={diagram}
                key={`${diagram.kind}-${diagram.title}`}
              />
            ))}
          </div>
        )}
        {activeTab === "moduleImages" && (
          <ModuleImagesPanel design={design} />
        )}
      </div>
    </section>
  );
}

function OverviewTab({ blueprint }: { blueprint: DesignBlueprint }) {
  return (
    <div className="bsm-content-stack">
      <InfoBlock
        title="Recommended architecture"
        text={blueprint.recommendedArchitecture}
        featured
      />
      {blueprint.costEstimate && (
        <CostEstimateBlock cost={blueprint.costEstimate} />
      )}
      <div className="bsm-info-grid">
        <InfoBlock title="Scaling advice" text={blueprint.scalingAdvice} />
        <InfoBlock title="Security notes" text={blueprint.securityNotes} />
      </div>
      <ListBlock title="Next steps" items={blueprint.nextSteps} ordered />
    </div>
  );
}

function ArchitectureTab({ blueprint }: { blueprint: DesignBlueprint }) {
  return (
    <div className="bsm-content-stack">
      <ListBlock title="Main components" items={blueprint.mainComponents} />
      <DeploymentServicesBlock services={blueprint.deploymentServices ?? []} />
      <div className="bsm-info-grid">
        <InfoBlock
          title="Database and storage"
          text={blueprint.databaseStorageRecommendation}
        />
        <InfoBlock
          title="API and backend"
          text={blueprint.apiBackendRecommendation}
        />
      </div>
    </div>
  );
}

function RisksTab({ blueprint }: { blueprint: DesignBlueprint }) {
  return (
    <div className="bsm-content-stack">
      <ListBlock title="Risks and tradeoffs" items={blueprint.risksTradeoffs} />
      <InfoBlock title="Security notes" text={blueprint.securityNotes} />
    </div>
  );
}

function DeploymentServicesBlock({
  services
}: {
  services: DeploymentService[];
}) {
  if (services.length === 0) {
    return (
      <InfoBlock
        title="Module deployment services"
        text="This blueprint predates deployment mapping. Generate or import a new result to receive concrete module runtimes."
      />
    );
  }

  return (
    <section className="bsm-section-card">
      <header className="bsm-section-heading">
        <div>
          <span className="bsm-kicker">Runtime map</span>
          <h3>Module deployment services</h3>
        </div>
        <button
          className="bsm-icon-button"
          type="button"
          title="Copy deployment mapping"
          aria-label="Copy deployment mapping"
          onClick={() => void copyText(deploymentServicesToMarkdown(services))}
        >
          <i className="bi bi-copy" />
        </button>
      </header>
      <div className="bsm-table-wrap">
        <table>
          <thead>
            <tr>
              <th>Module</th>
              <th>Service</th>
              <th>Runtime</th>
              <th>Why</th>
            </tr>
          </thead>
          <tbody>
            {services.map((service) => (
              <tr key={`${service.module}-${service.recommendedService}`}>
                <td><strong>{service.module}</strong></td>
                <td>{service.recommendedService}</td>
                <td>{service.runtime}</td>
                <td>{service.reason}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function CostEstimateBlock({ cost }: { cost: CostEstimate }) {
  return (
    <section className="bsm-section-card bsm-cost-card">
      <header className="bsm-cost-heading">
        <div>
          <span className="bsm-kicker">Infrastructure estimate · {cost.currency}</span>
          <h3>Total estimated cost</h3>
          <p>{cost.summary}</p>
        </div>
        <strong>{cost.monthlyRange}</strong>
      </header>
      <div className="bsm-table-wrap">
        <table>
          <thead>
            <tr><th>Item</th><th>Monthly</th><th>Notes</th></tr>
          </thead>
          <tbody>
            {cost.lineItems.map((item) => (
              <tr key={item.name}>
                <td>{item.name}</td>
                <td><strong>{item.monthlyRange}</strong></td>
                <td>{item.notes}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="bsm-info-grid">
        <ListBlock title="Assumptions" items={cost.assumptions} />
        <ListBlock title="Cost optimizations" items={cost.costOptimizations} />
      </div>
    </section>
  );
}

function InfoBlock({
  title,
  text,
  featured = false
}: {
  title: string;
  text: string;
  featured?: boolean;
}) {
  return (
    <section className={`bsm-info-block${featured ? " featured" : ""}`}>
      <h3>{title}</h3>
      <p>{text}</p>
    </section>
  );
}

function ListBlock({
  title,
  items,
  ordered = false
}: {
  title: string;
  items: string[];
  ordered?: boolean;
}) {
  const List = ordered ? "ol" : "ul";
  return (
    <section className="bsm-info-block">
      <h3>{title}</h3>
      <List className={ordered ? "bsm-step-list" : "bsm-plain-list"}>
        {items.map((item) => <li key={item}>{item}</li>)}
      </List>
    </section>
  );
}

function BlueprintEmptyState() {
  return (
    <section className="bsm-card bsm-blueprint-empty">
      <div className="bsm-orbit" aria-hidden="true">
        <span><i className="bi bi-lightbulb" /></span>
        <i className="bi bi-boxes" />
        <i className="bi bi-database" />
        <i className="bi bi-cloud" />
      </div>
      <span className="bsm-kicker">Blank canvas</span>
      <h2>Turn an idea into a system blueprint</h2>
      <p>
        Fill in the brief to explore architecture, deployment services,
        API contracts, batch jobs, schemas, costs, risks, and diagrams.
      </p>
      <div className="bsm-empty-capabilities">
        {[
          ["bi-diagram-3", "Architecture"],
          ["bi-hdd-network", "Services & APIs"],
          ["bi-images", "Module images"],
          ["bi-table", "Data model"],
          ["bi-cash-stack", "Cost"],
          ["bi-shield-check", "Risk"]
        ].map(([icon, label]) => (
          <span key={label}><i className={`bi ${icon}`} /> {label}</span>
        ))}
      </div>
    </section>
  );
}
