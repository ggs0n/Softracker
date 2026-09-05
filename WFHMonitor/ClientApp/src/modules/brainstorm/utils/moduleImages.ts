import type {
  BatchJob,
  DeploymentService,
  DesignProjectDetails,
  GrpcContract,
  RestEndpoint,
  ServiceDetail,
  TableSchema
} from "../types";
import { escapeXml } from "./diagrams";

export const MAX_MODULE_IMAGES = 10;

export interface ModuleArchitectureImage {
  moduleName: string;
  svg: string;
  restEndpointCount: number;
  grpcContractCount: number;
  tableCount: number;
  batchJobCount: number;
}

interface ModuleImageContext {
  moduleName: string;
  service: ServiceDetail | null;
  deployment: DeploymentService | null;
  restEndpoints: RestEndpoint[];
  grpcContracts: GrpcContract[];
  schemas: TableSchema[];
  batchJobs: BatchJob[];
}

export function buildModuleArchitectureImages(
  design: DesignProjectDetails
): ModuleArchitectureImage[] {
  const blueprint = design.blueprint;
  return extractMainModuleNames(design)
    .slice(0, MAX_MODULE_IMAGES)
    .map((moduleName) => {
      const context: ModuleImageContext = {
        moduleName,
        service: findRelated(
          blueprint.serviceDetails ?? [],
          moduleName,
          serviceSearchText
        ),
        deployment: findRelated(
          blueprint.deploymentServices ?? [],
          moduleName,
          deploymentSearchText
        ),
        restEndpoints: findAllRelated(
          blueprint.restEndpoints ?? [],
          moduleName,
          restEndpointSearchText
        ),
        grpcContracts: findAllRelated(
          blueprint.grpcContracts ?? [],
          moduleName,
          grpcSearchText
        ),
        schemas: findAllRelated(
          blueprint.tableSchemas ?? [],
          moduleName,
          schemaSearchText
        ),
        batchJobs: findAllRelated(
          blueprint.batchJobs ?? [],
          moduleName,
          batchJobSearchText
        )
      };

      return {
        moduleName,
        svg: buildModuleArchitectureSvg(context),
        restEndpointCount: context.restEndpoints.length,
        grpcContractCount: context.grpcContracts.length,
        tableCount: context.schemas.length,
        batchJobCount: context.batchJobs.length
      };
    });
}

function extractMainModuleNames(design: DesignProjectDetails): string[] {
  const featureModules = uniqueNames(
    design.features
      .split(/[,;\n\r]+/)
      .map(cleanModuleName)
      .filter(Boolean)
  );
  if (featureModules.length > 0) return featureModules;

  const domainServices = uniqueNames(
    (design.blueprint.serviceDetails ?? [])
      .filter((service) =>
        /(domain|module|microservice|application|api)/i.test(service.type)
      )
      .map((service) => cleanModuleName(service.name))
      .filter(Boolean)
  );
  if (domainServices.length > 0) return domainServices;

  return uniqueNames(
    design.blueprint.mainComponents
      .map(cleanModuleName)
      .filter(Boolean)
  );
}

function cleanModuleName(value: string): string {
  const cleaned = value
    .replace(/\b(capability|domain module|microservice|module)\b$/i, "")
    .replace(/\s+/g, " ")
    .trim();
  return cleaned.length === 0
    ? ""
    : cleaned.charAt(0).toLocaleUpperCase() + cleaned.slice(1);
}

function uniqueNames(values: string[]): string[] {
  const seen = new Set<string>();
  return values.filter((value) => {
    const key = normalize(value);
    if (!key || seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function findRelated<T>(
  values: T[],
  moduleName: string,
  searchText: (value: T) => string
): T | null {
  return findAllRelated(values, moduleName, searchText)[0] ?? null;
}

function findAllRelated<T>(
  values: T[],
  moduleName: string,
  searchText: (value: T) => string
): T[] {
  return values.filter((value) =>
    isRelated(searchText(value), moduleName)
  );
}

function isRelated(value: string, moduleName: string): boolean {
  const candidate = normalize(value);
  const moduleKey = normalize(moduleName);
  if (!candidate || !moduleKey) return false;
  if (candidate.includes(moduleKey)) return true;

  const words = moduleName
    .toLocaleLowerCase()
    .split(/[^a-z0-9]+/)
    .filter((word) => word.length > 2);
  return words.length > 0 && words.every((word) => candidate.includes(word));
}

function normalize(value: string): string {
  return value.toLocaleLowerCase().replace(/[^a-z0-9]+/g, "");
}

function serviceSearchText(service: ServiceDetail): string {
  return [
    service.name,
    service.responsibility,
    service.dataOwnership,
    ...service.dependencies,
    ...service.communication
  ].join(" ");
}

function deploymentSearchText(deployment: DeploymentService): string {
  return [
    deployment.module,
    deployment.recommendedService,
    deployment.runtime,
    deployment.reason
  ].join(" ");
}

function restEndpointSearchText(endpoint: RestEndpoint): string {
  return [
    endpoint.service,
    endpoint.path,
    endpoint.purpose,
    endpoint.request,
    endpoint.response
  ].join(" ");
}

function grpcSearchText(contract: GrpcContract): string {
  return [
    contract.service,
    contract.contract,
    contract.rpcMethod,
    contract.purpose
  ].join(" ");
}

function schemaSearchText(schema: TableSchema): string {
  return [
    schema.name,
    schema.purpose,
    ...schema.columns.flatMap((column) => [column.name, column.notes]),
    ...schema.relationships
  ].join(" ");
}

function batchJobSearchText(job: BatchJob): string {
  return [
    job.name,
    job.ownerService,
    job.responsibility,
    job.input,
    job.output
  ].join(" ");
}

function buildModuleArchitectureSvg(context: ModuleImageContext): string {
  const title = context.service?.name ?? `${context.moduleName} Module`;
  const runtime =
    context.service?.runtime
    ?? context.deployment?.runtime
    ?? "Application service";
  const deployment =
    context.service?.deployment
    ?? context.deployment?.recommendedService
    ?? "Configured application runtime";
  const responsibility =
    context.service?.responsibility
    ?? `Implements the complete ${context.moduleName} workflow.`;
  const endpointLines = [
    ...context.restEndpoints.slice(0, 3).map((endpoint) =>
      `${endpoint.method} ${endpoint.path}`
    ),
    ...context.grpcContracts.slice(0, 2).map((contract) =>
      `gRPC ${contract.rpcMethod}`
    )
  ];
  const dataLines = context.schemas
    .slice(0, 4)
    .map((schema) => schema.name);
  const dependencyLines = context.service?.dependencies.slice(0, 3) ?? [];
  const jobLines = context.batchJobs
    .slice(0, 3)
    .map((job) => `${job.name} · ${job.trigger}`);

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1000 620" width="1000" height="620" role="img" aria-label="${escapeXml(context.moduleName)} module architecture image">
    <defs>
      <pattern id="module-grid" width="24" height="24" patternUnits="userSpaceOnUse">
        <path d="M 24 0 L 0 0 0 24" fill="none" stroke="#e9eeea" stroke-width="1"/>
      </pattern>
      <marker id="module-arrow" markerWidth="9" markerHeight="9" refX="8" refY="4.5" orient="auto">
        <path d="M0,0 L9,4.5 L0,9 z" fill="#49645a"/>
      </marker>
      <filter id="module-shadow" x="-20%" y="-20%" width="140%" height="140%">
        <feDropShadow dx="0" dy="8" stdDeviation="10" flood-color="#17231f" flood-opacity=".10"/>
      </filter>
    </defs>
    <style>
      .bg{fill:#fbfcf9}.grid{fill:url(#module-grid)}
      .eyebrow{fill:#b64f35;font:700 13px Inter,Arial,sans-serif;letter-spacing:1.4px}
      .heading{fill:#17231f;font:800 32px Outfit,Inter,Arial,sans-serif}
      .subheading{fill:#66736d;font:15px Inter,Arial,sans-serif}
      .panel{fill:#fff;stroke:#ccd6d0;stroke-width:1.5}
      .module{fill:#1c3b32;stroke:#142c26;stroke-width:2;filter:url(#module-shadow)}
      .panel-title{fill:#17231f;font:800 17px Outfit,Inter,Arial,sans-serif}
      .module-title{fill:#fff;font:800 24px Outfit,Inter,Arial,sans-serif;text-anchor:middle}
      .module-meta{fill:#cfe2da;font:600 13px Inter,Arial,sans-serif;text-anchor:middle}
      .body{fill:#52635b;font:14px Inter,Arial,sans-serif}
      .module-body{fill:#eff7f3;font:14px Inter,Arial,sans-serif;text-anchor:middle}
      .empty{fill:#8a9690;font:italic 13px Inter,Arial,sans-serif}
      .flow{fill:none;stroke:#49645a;stroke-width:2.5;marker-end:url(#module-arrow)}
      .flow-label{fill:#607068;font:700 11px Inter,Arial,sans-serif;text-anchor:middle}
      .job{fill:#fff5ed;stroke:#e2b9aa;stroke-width:1.5}
      .count{fill:#e05a3b}.count-text{fill:#fff;font:800 12px Inter,Arial,sans-serif;text-anchor:middle;dominant-baseline:central}
    </style>
    <rect width="1000" height="620" class="bg"/>
    <rect width="1000" height="620" class="grid"/>
    <text x="50" y="50" class="eyebrow">MAIN MODULE IMAGE</text>
    <text x="50" y="88" class="heading">${escapeXml(context.moduleName)}</text>
    <text x="50" y="116" class="subheading">Only contracts, data, dependencies, and jobs related to this module are shown.</text>

    <path d="M 280 310 L 350 310" class="flow"/>
    <text x="315" y="294" class="flow-label">CALLS</text>
    <path d="M 650 310 L 720 310" class="flow"/>
    <text x="685" y="294" class="flow-label">OWNS</text>
    <path d="M 500 455 L 500 505" class="flow"/>

    <rect x="50" y="155" width="230" height="310" rx="16" class="panel"/>
    <circle cx="250" cy="184" r="15" class="count"/>
    <text x="250" y="184" class="count-text">${endpointLines.length}</text>
    <text x="76" y="194" class="panel-title">Interfaces</text>
    ${svgTextLines(
      endpointLines.length > 0 ? endpointLines : ["No related API contract"],
      76,
      232,
      24,
      endpointLines.length > 0 ? "body" : "empty",
      27
    )}
    <text x="76" y="372" class="panel-title">Dependencies</text>
    ${svgTextLines(
      dependencyLines.length > 0
        ? dependencyLines
        : ["No related dependency"],
      76,
      407,
      23,
      dependencyLines.length > 0 ? "body" : "empty",
      27
    )}

    <rect x="350" y="155" width="300" height="310" rx="20" class="module"/>
    <text x="500" y="205" class="module-title">${escapeXml(title)}</text>
    <text x="500" y="237" class="module-meta">${escapeXml(runtime)}</text>
    <text x="500" y="261" class="module-meta">${escapeXml(deployment)}</text>
    ${svgTextLines(
      wrapText(responsibility, 38).slice(0, 5),
      500,
      320,
      25,
      "module-body",
      38
    )}

    <rect x="720" y="155" width="230" height="310" rx="16" class="panel"/>
    <circle cx="920" cy="184" r="15" class="count"/>
    <text x="920" y="184" class="count-text">${dataLines.length}</text>
    <text x="746" y="194" class="panel-title">Owned data</text>
    ${svgTextLines(
      dataLines.length > 0 ? dataLines : ["No related table"],
      746,
      232,
      24,
      dataLines.length > 0 ? "body" : "empty",
      27
    )}
    <text x="746" y="372" class="panel-title">Ownership rule</text>
    ${svgTextLines(
      wrapText(
        context.service?.dataOwnership
          ?? `Owns authoritative ${context.moduleName} records.`,
        27
      ).slice(0, 3),
      746,
      407,
      22,
      "body",
      27
    )}

    <rect x="220" y="505" width="560" height="82" rx="14" class="job"/>
    <circle cx="750" cy="532" r="15" class="count"/>
    <text x="750" y="532" class="count-text">${jobLines.length}</text>
    <text x="246" y="537" class="panel-title">Batch / background work</text>
    ${svgTextLines(
      jobLines.length > 0 ? jobLines : ["No related batch job"],
      246,
      565,
      19,
      jobLines.length > 0 ? "body" : "empty",
      65
    )}
  </svg>`;
}

function svgTextLines(
  values: string[],
  x: number,
  y: number,
  lineHeight: number,
  className: string,
  maxCharacters: number
): string {
  const lines = values.flatMap((value) => wrapText(value, maxCharacters));
  return lines
    .slice(0, 6)
    .map((line, index) =>
      `<text x="${x}" y="${y + index * lineHeight}" class="${className}">${escapeXml(line)}</text>`
    )
    .join("");
}

function wrapText(value: string, maxCharacters: number): string[] {
  const words = value.replace(/\s+/g, " ").trim().split(" ");
  const lines: string[] = [];
  let current = "";
  for (const word of words) {
    const next = current ? `${current} ${word}` : word;
    if (current && next.length > maxCharacters) {
      lines.push(current);
      current = word;
    } else {
      current = next;
    }
  }
  if (current) lines.push(current);
  return lines.length > 0 ? lines : [value];
}
