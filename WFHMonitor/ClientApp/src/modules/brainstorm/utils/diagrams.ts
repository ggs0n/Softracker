import type {
  DeploymentService,
  TableSchema
} from "../types";
import { shortType } from "./exports";

interface PositionedTable {
  schema: TableSchema;
  x: number;
  y: number;
  width: number;
  height: number;
}

export function buildSchemaDiagramSvg(schemas: TableSchema[]): string {
  const tableWidth = 290;
  const headerHeight = 48;
  const rowHeight = 34;
  const gapX = 135;
  const gapY = 74;
  const margin = 48;
  const columnCount = Math.min(
    3,
    Math.max(1, Math.ceil(Math.sqrt(schemas.length)))
  );
  const positioned: PositionedTable[] = schemas.map((schema, index) => {
    const gridColumn = index % columnCount;
    const gridRow = Math.floor(index / columnCount);
    const height = headerHeight + Math.max(schema.columns.length, 1) * rowHeight;
    return {
      schema,
      x: margin + gridColumn * (tableWidth + gapX),
      y: margin + gridRow * (height + gapY),
      width: tableWidth,
      height
    };
  });

  const width =
    margin * 2 +
    columnCount * tableWidth +
    Math.max(0, columnCount - 1) * gapX;
  const rowCount = Math.ceil(schemas.length / columnCount);
  const tallest = Math.max(...positioned.map((table) => table.height), 220);
  const height =
    margin * 2 + rowCount * tallest + Math.max(0, rowCount - 1) * gapY;
  const lookup = new Map(
    positioned.map((table) => [normalizeTableName(table.schema.name), table])
  );

  const paths = buildSchemaRelationships(positioned, lookup)
    .map((relationship, index) => {
      const startX = relationship.from.x + relationship.from.width;
      const startY =
        relationship.from.y +
        headerHeight +
        relationship.fromRow * rowHeight +
        rowHeight / 2;
      const endX = relationship.to.x;
      const endY =
        relationship.to.y +
        headerHeight +
        relationship.toRow * rowHeight +
        rowHeight / 2;
      const controlX =
        startX + Math.max(70, Math.abs(endX - startX) / 2) + (index % 5) * 10;
      return `<path d="M ${startX} ${startY} C ${controlX} ${startY}, ${controlX} ${endY}, ${endX} ${endY}" fill="none" stroke="#b9c2bd" stroke-width="2.2"/>`;
    })
    .join("");

  const tables = positioned
    .map((table) => {
      const rows = table.schema.columns
        .map((column, index) => {
          const y = table.y + headerHeight + index * rowHeight;
          const key = column.isPrimaryKey
            ? "PK"
            : column.isForeignKey
              ? "FK"
              : "";
          return `<text x="${table.x + 20}" y="${y + 23}" class="schema-column">${escapeXml(column.name)}</text>
            <text x="${table.x + table.width - 18}" y="${y + 23}" class="schema-type">${escapeXml(shortType(column.type))}${key ? ` ${key}` : ""}</text>`;
        })
        .join("");
      return `<g>
        <rect x="${table.x}" y="${table.y}" width="${table.width}" height="${table.height}" class="schema-body"/>
        <rect x="${table.x}" y="${table.y}" width="${table.width}" height="${headerHeight}" class="schema-header"/>
        <text x="${table.x + 20}" y="${table.y + 31}" class="schema-title">${escapeXml(table.schema.name)}</text>
        ${rows}
      </g>`;
    })
    .join("");

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${width} ${height}" width="${width}" height="${height}" role="img" aria-label="Database schema diagram">
    <style>
      .schema-bg{fill:#f8faf9}.schema-body{fill:#f1f2ef;stroke:#d7dcd8;stroke-width:1}
      .schema-header{fill:#214b3f}.schema-title{fill:#fff;font:700 20px Arial,sans-serif}
      .schema-column{fill:#17231f;font:18px Arial,sans-serif}
      .schema-type{fill:#68736e;font:600 15px Arial,sans-serif;text-anchor:end}
    </style>
    <rect width="${width}" height="${height}" class="schema-bg"/>
    ${paths}${tables}
  </svg>`;
}

export function buildCloudDeploymentSvg(
  services: DeploymentService[]
): string {
  const find = (module: string, fallback: string, field: "service" | "runtime") => {
    const match = services.find((item) => item.module === module);
    return field === "service"
      ? match?.recommendedService ?? fallback
      : match?.runtime ?? fallback;
  };
  const provider = resolveProviderName(services);
  const nodes = [
    [80, 300, "User", "Browser / mobile", "neutral"],
    [300, 300, find("Customer / Admin Web Client", "Static hosting", "service"), find("Customer / Admin Web Client", "React client", "runtime"), "service"],
    [540, 150, find("Authentication Layer", "Identity provider", "service"), find("Authentication Layer", "OIDC / JWT", "runtime"), "auth"],
    [540, 300, find("API Gateway / Backend API", "API Gateway", "service"), find("API Gateway / Backend API", ".NET API", "runtime"), "service"],
    [800, 300, find("Domain Modules", "Domain modules", "service"), find("Domain Modules", ".NET runtime", "runtime"), "compute"],
    [1060, 180, find("Database", "Managed database", "service"), find("Database", "SQL", "runtime"), "data"],
    [1060, 320, find("Cache / Queue", "Queue / cache", "service"), find("Cache / Queue", "Managed service", "runtime"), "data"],
    [1060, 460, find("Background Worker", "Worker", "service"), find("Background Worker", "Background runtime", "runtime"), "compute"]
  ] as const;
  const renderedNodes = nodes
    .map(([x, y, title, subtitle, kind]) => cloudNode(x, y, title, subtitle, kind))
    .join("");
  const arrowDefinitions: Array<[number, number, number, number, number]> = [
    [230, 342, 300, 342, 1],
    [450, 320, 540, 320, 2],
    [375, 300, 540, 205, 3],
    [690, 342, 800, 342, 4],
    [950, 326, 1060, 222, 5],
    [950, 342, 1060, 362, 6],
    [950, 358, 1060, 502, 7]
  ];
  const arrows = arrowDefinitions
    .map(
      ([x1, y1, x2, y2, number]) =>
        `<path d="M${x1} ${y1} L${x2} ${y2}" class="cloud-line"/>${flowMarker((x1 + x2) / 2, (y1 + y2) / 2, number)}`
    )
    .join("");

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1320 650" width="1320" height="650" role="img" aria-label="Cloud deployment template">
    <defs><marker id="brainstorm-arrow" markerWidth="10" markerHeight="10" refX="9" refY="5" orient="auto"><path d="M0 0L10 5L0 10z" fill="#66768d"/></marker></defs>
    <style>
      .cloud-bg{fill:#f7faf8}.cloud-boundary{fill:#fff;stroke:#263b34;stroke-width:2.4}
      .product-boundary{fill:#eff7f3;stroke:#b8cdc4;stroke-width:1.7}
      .managed-boundary{fill:#fff8f3;stroke:#dfc0aa;stroke-width:1.7}
      .cloud-label{fill:#17231f;font:700 18px Arial,sans-serif}.cloud-small{fill:#53615b;font:700 13px Arial,sans-serif}
      .cloud-node{fill:#fff;stroke:#7b8983;stroke-width:1.7}.cloud-auth{fill:#fff5f8;stroke:#c53d70;stroke-width:2}
      .cloud-compute{fill:#fff4e9;stroke:#d85b24;stroke-width:2}.cloud-data{fill:#f4faec;stroke:#7b9d2f;stroke-width:2}
      .cloud-title{fill:#17231f;font:700 14px Arial,sans-serif;text-anchor:middle}.cloud-subtitle{fill:#66736d;font:12px Arial,sans-serif;text-anchor:middle}
      .cloud-line{fill:none;stroke:#66768d;stroke-width:2;marker-end:url(#brainstorm-arrow)}
      .flow{fill:#17231f}.flow-text{fill:#fff;font:700 11px Arial,sans-serif;text-anchor:middle;dominant-baseline:central}
    </style>
    <rect width="1320" height="650" class="cloud-bg"/>
    <rect x="24" y="24" width="1272" height="602" rx="12" class="cloud-boundary"/>
    <text x="54" y="65" class="cloud-label">${escapeXml(provider)}</text>
    <rect x="50" y="92" width="920" height="500" rx="8" class="product-boundary"/>
    <text x="76" y="124" class="cloud-small">CUSTOMER / PRODUCT ACCOUNT</text>
    <rect x="990" y="92" width="280" height="500" rx="8" class="managed-boundary"/>
    <text x="1014" y="124" class="cloud-small">MANAGED SERVICES</text>
    ${arrows}${renderedNodes}
    <rect x="1020" y="545" width="210" height="30" rx="5" class="cloud-node"/>
    <text x="1125" y="565" class="cloud-subtitle">Logs · secrets · registry · backups</text>
  </svg>`;
}

export async function downloadDiagramPng(
  svgMarkup: string,
  title: string
): Promise<void> {
  const parser = new DOMParser();
  const parsedSvg = parser
    .parseFromString(svgMarkup, "image/svg+xml")
    .querySelector("svg");
  if (!parsedSvg) return;

  const viewBox = parsedSvg
    .getAttribute("viewBox")
    ?.split(/\s+/)
    .map(Number);
  const width =
    viewBox?.length === 4 && Number.isFinite(viewBox[2])
      ? Math.max(viewBox[2] ?? 0, 900)
      : 1600;
  const height =
    viewBox?.length === 4 && Number.isFinite(viewBox[3])
      ? Math.max(viewBox[3] ?? 0, 520)
      : 900;
  parsedSvg.setAttribute("width", String(width));
  parsedSvg.setAttribute("height", String(height));

  const blob = new Blob([new XMLSerializer().serializeToString(parsedSvg)], {
    type: "image/svg+xml;charset=utf-8"
  });
  const sourceUrl = URL.createObjectURL(blob);
  try {
    const image = new Image();
    await new Promise<void>((resolve, reject) => {
      image.onload = () => resolve();
      image.onerror = () => reject(new Error("Diagram image could not be prepared."));
      image.src = sourceUrl;
    });
    const canvas = document.createElement("canvas");
    canvas.width = Math.ceil(width * 2);
    canvas.height = Math.ceil(height * 2);
    const context = canvas.getContext("2d");
    if (!context) return;
    context.fillStyle = "#ffffff";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    const png = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob(resolve, "image/png")
    );
    if (!png) return;
    const pngUrl = URL.createObjectURL(png);
    const anchor = document.createElement("a");
    anchor.href = pngUrl;
    anchor.download = `${slugifyFileName(title)}.png`;
    anchor.click();
    URL.revokeObjectURL(pngUrl);
  } finally {
    URL.revokeObjectURL(sourceUrl);
  }
}

export function escapeXml(value: string): string {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

function buildSchemaRelationships(
  tables: PositionedTable[],
  lookup: Map<string, PositionedTable>
) {
  return tables.flatMap((from) =>
    from.schema.columns.flatMap((column, fromRow) => {
      const target = inferRelationshipTarget(column.name, lookup);
      if (!target || target.schema.name === from.schema.name) return [];
      const primaryIndex = target.schema.columns.findIndex(
        (candidate) => candidate.isPrimaryKey
      );
      return [{
        from,
        to: target,
        fromRow,
        toRow: primaryIndex < 0 ? 0 : primaryIndex
      }];
    })
  );
}

function inferRelationshipTarget(
  columnName: string,
  lookup: Map<string, PositionedTable>
): PositionedTable | undefined {
  const normalized = normalizeTableName(columnName);
  if (!normalized.endsWith("id")) return undefined;
  const baseName = normalized.replace(/id$/, "");
  return [baseName, `${baseName}s`, `${baseName}es`]
    .map((candidate) => lookup.get(candidate))
    .find(Boolean);
}

function normalizeTableName(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]/g, "");
}

function cloudNode(
  x: number,
  y: number,
  title: string,
  subtitle: string,
  kind: string
): string {
  const css =
    kind === "auth"
      ? "cloud-auth"
      : kind === "compute"
        ? "cloud-compute"
        : kind === "data"
          ? "cloud-data"
          : "cloud-node";
  return `<g><rect x="${x}" y="${y}" width="150" height="84" rx="7" class="${css}"/>
    <text x="${x + 75}" y="${y + 35}" class="cloud-title">${escapeXml(shortService(title))}</text>
    <text x="${x + 75}" y="${y + 57}" class="cloud-subtitle">${escapeXml(shortService(subtitle))}</text></g>`;
}

function flowMarker(x: number, y: number, value: number): string {
  return `<g><circle cx="${x}" cy="${y}" r="11" class="flow"/><text x="${x}" y="${y}" class="flow-text">${value}</text></g>`;
}

function resolveProviderName(services: DeploymentService[]): string {
  const combined = services
    .map((service) => `${service.recommendedService} ${service.runtime}`)
    .join(" ")
    .toLowerCase();
  if (combined.includes("aws") || combined.includes("amazon"))
    return "AWS Cloud";
  if (combined.includes("azure")) return "Microsoft Azure Cloud";
  return "Cloud Deployment";
}

function shortService(value: string): string {
  const shortened = value
    .replace("Microsoft ", "")
    .replace("Application Load Balancer + ", "")
    .replace("Azure AD B2C / Entra External ID", "Entra External ID")
    .replace("Amazon RDS SQL Server/PostgreSQL", "Amazon RDS")
    .replace("Azure Service Bus + Azure Cache for Redis", "Service Bus + Redis");
  return shortened.length > 24 ? `${shortened.slice(0, 22)}…` : shortened;
}

function slugifyFileName(value: string): string {
  return (
    value
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/(^-|-$)/g, "")
      .slice(0, 60) || "diagram"
  );
}
