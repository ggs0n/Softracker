import type {
  BatchJob,
  GrpcContract,
  RestEndpoint,
  ServiceDetail
} from "../types";
import { copyText } from "../utils/exports";

interface ServicesApiPanelProps {
  services: ServiceDetail[];
  restEndpoints: RestEndpoint[];
  grpcContracts: GrpcContract[];
  batchJobs: BatchJob[];
}

export function ServicesApiPanel({
  services,
  restEndpoints,
  grpcContracts,
  batchJobs
}: ServicesApiPanelProps) {
  const hasDetails =
    services.length > 0
    || restEndpoints.length > 0
    || grpcContracts.length > 0
    || batchJobs.length > 0;

  if (!hasDetails) {
    return (
      <section className="bsm-info-block">
        <h3>Services &amp; APIs</h3>
        <p>
          This saved blueprint predates service-contract details. Generate or
          import a new blueprint to receive REST, gRPC, and batch-job designs.
        </p>
      </section>
    );
  }

  return (
    <div className="bsm-content-stack">
      <ServiceCatalog services={services} />
      <RestCatalog endpoints={restEndpoints} />
      <GrpcCatalog contracts={grpcContracts} />
      <BatchCatalog jobs={batchJobs} />
    </div>
  );
}

function ServiceCatalog({ services }: { services: ServiceDetail[] }) {
  return (
    <section className="bsm-section-card">
      <PanelHeading
        kicker="Service catalog"
        title="Microservices and deployable modules"
        copyValue={services}
      />
      {services.length === 0 ? (
        <EmptyMessage text="No separately described services are required." />
      ) : (
        <div className="bsm-service-grid">
          {services.map((service) => (
            <article className="bsm-service-card" key={service.name}>
              <header>
                <div>
                  <span>{service.type}</span>
                  <h4>{service.name}</h4>
                </div>
                <i className="bi bi-box" aria-hidden="true" />
              </header>
              <p>{service.responsibility}</p>
              <dl className="bsm-contract-details">
                <Detail label="Runtime" value={service.runtime} />
                <Detail label="Deployment" value={service.deployment} />
                <Detail label="Data ownership" value={service.dataOwnership} wide />
              </dl>
              <TagGroup label="Communication" values={service.communication} />
              <TagGroup label="Dependencies" values={service.dependencies} />
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function RestCatalog({ endpoints }: { endpoints: RestEndpoint[] }) {
  return (
    <section className="bsm-section-card">
      <PanelHeading
        kicker="HTTP interface"
        title="REST API endpoints"
        copyValue={endpoints}
      />
      {endpoints.length === 0 ? (
        <EmptyMessage text="No public REST boundary is recommended." />
      ) : (
        <div className="bsm-contract-list">
          {endpoints.map((endpoint, index) => (
            <article
              className="bsm-contract-card"
              key={`${endpoint.service}-${endpoint.method}-${endpoint.path}-${index}`}
            >
              <header>
                <span className={`bsm-method bsm-method-${endpoint.method.toLowerCase()}`}>
                  {endpoint.method}
                </span>
                <code>{endpoint.path}</code>
                <small>{endpoint.service}</small>
              </header>
              <p>{endpoint.purpose}</p>
              <dl className="bsm-contract-details">
                <Detail label="Authentication" value={endpoint.authentication} />
                <Detail label="Status codes" value={endpoint.statusCodes.join(", ")} />
                <Detail label="Request" value={endpoint.request} />
                <Detail label="Response" value={endpoint.response} />
              </dl>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function GrpcCatalog({ contracts }: { contracts: GrpcContract[] }) {
  return (
    <section className="bsm-section-card">
      <PanelHeading
        kicker="Internal RPC"
        title="gRPC contracts"
        copyValue={contracts}
      />
      {contracts.length === 0 ? (
        <EmptyMessage text="No gRPC boundary is justified; use in-process calls or REST for this design." />
      ) : (
        <div className="bsm-contract-list">
          {contracts.map((contract, index) => (
            <article
              className="bsm-contract-card bsm-grpc-card"
              key={`${contract.contract}-${contract.rpcMethod}-${index}`}
            >
              <header>
                <span className="bsm-method bsm-method-grpc">gRPC</span>
                <code>{contract.contract}/{contract.rpcMethod}</code>
                <small>{contract.streaming}</small>
              </header>
              <p>{contract.purpose}</p>
              <dl className="bsm-contract-details">
                <Detail label="Owner" value={contract.service} />
                <Detail label="Streaming" value={contract.streaming} />
                <Detail label="Request message" value={contract.requestMessage} />
                <Detail label="Response message" value={contract.responseMessage} />
              </dl>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function BatchCatalog({ jobs }: { jobs: BatchJob[] }) {
  return (
    <section className="bsm-section-card">
      <PanelHeading
        kicker="Background processing"
        title="Batch and scheduled jobs"
        copyValue={jobs}
      />
      {jobs.length === 0 ? (
        <EmptyMessage text="No scheduled or queue-triggered jobs are required." />
      ) : (
        <div className="bsm-service-grid">
          {jobs.map((job) => (
            <article className="bsm-service-card bsm-job-card" key={job.name}>
              <header>
                <div>
                  <span>{job.trigger} · {job.schedule}</span>
                  <h4>{job.name}</h4>
                </div>
                <i className="bi bi-clock-history" aria-hidden="true" />
              </header>
              <p>{job.responsibility}</p>
              <dl className="bsm-contract-details">
                <Detail label="Owner" value={job.ownerService} />
                <Detail label="Input" value={job.input} />
                <Detail label="Output" value={job.output} />
                <Detail label="Retry policy" value={job.retryPolicy} />
                <Detail
                  label="Idempotency"
                  value={job.idempotencyStrategy}
                  wide
                />
              </dl>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function PanelHeading({
  kicker,
  title,
  copyValue
}: {
  kicker: string;
  title: string;
  copyValue: unknown;
}) {
  return (
    <header className="bsm-section-heading bsm-panel-heading">
      <div>
        <span className="bsm-kicker">{kicker}</span>
        <h3>{title}</h3>
      </div>
      <button
        className="bsm-icon-button"
        type="button"
        title={`Copy ${title}`}
        aria-label={`Copy ${title}`}
        onClick={() => void copyText(JSON.stringify(copyValue, null, 2))}
      >
        <i className="bi bi-copy" />
      </button>
    </header>
  );
}

function Detail({
  label,
  value,
  wide = false
}: {
  label: string;
  value: string;
  wide?: boolean;
}) {
  return (
    <div className={wide ? "wide" : undefined}>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function TagGroup({ label, values }: { label: string; values: string[] }) {
  if (values.length === 0) return null;
  return (
    <div className="bsm-tag-group">
      <strong>{label}</strong>
      <div>
        {values.map((value) => <span key={value}>{value}</span>)}
      </div>
    </div>
  );
}

function EmptyMessage({ text }: { text: string }) {
  return <p className="bsm-empty-message">{text}</p>;
}
