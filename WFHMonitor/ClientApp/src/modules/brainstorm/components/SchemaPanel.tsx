import { useState } from "react";
import type { TableSchema } from "../types";
import {
  buildSchemaDiagramSvg,
  downloadDiagramPng
} from "../utils/diagrams";
import {
  copyText,
  tableSchemasToDbml,
  tableSchemaToMarkdown
} from "../utils/exports";

export function SchemaPanel({ schemas }: { schemas: TableSchema[] }) {
  if (schemas.length === 0) {
    return (
      <InfoMessage>
        This saved blueprint predates schema support. Generate or import a new
        one to receive starter tables.
      </InfoMessage>
    );
  }

  return (
    <div className="bsm-schema-list">
      <SchemaDiagram schemas={schemas} />
      {schemas.map((schema) => (
        <section className="bsm-section-card" key={schema.name}>
          <header className="bsm-section-heading">
            <div>
              <span className="bsm-kicker">Table</span>
              <h3>{schema.name}</h3>
              <p>{schema.purpose}</p>
            </div>
            <button
              className="bsm-icon-button"
              type="button"
              title="Copy table schema"
              aria-label={`Copy ${schema.name} schema`}
              onClick={() => void copyText(tableSchemaToMarkdown(schema))}
            >
              <i className="bi bi-copy" />
            </button>
          </header>
          <div className="bsm-table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Column</th>
                  <th>Type</th>
                  <th>Key</th>
                  <th>Notes</th>
                </tr>
              </thead>
              <tbody>
                {schema.columns.map((column) => (
                  <tr key={`${schema.name}-${column.name}`}>
                    <td><code>{column.name}</code></td>
                    <td>{column.type}</td>
                    <td>
                      {column.isPrimaryKey
                        ? "PK"
                        : column.isForeignKey
                          ? "FK"
                          : "—"}
                    </td>
                    <td>{column.notes}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="bsm-relationships">
            <strong>Relationships</strong>
            <ul>
              {schema.relationships.map((relationship) => (
                <li key={relationship}>{relationship}</li>
              ))}
            </ul>
          </div>
        </section>
      ))}
    </div>
  );
}

function SchemaDiagram({ schemas }: { schemas: TableSchema[] }) {
  const [isFullscreen, setIsFullscreen] = useState(false);
  const svg = buildSchemaDiagramSvg(schemas);

  return (
    <section className="bsm-section-card bsm-schema-diagram">
      <header className="bsm-section-heading">
        <div>
          <span className="bsm-kicker">Entity map</span>
          <h3>Database schema diagram</h3>
          <p>Relationships inferred from primary and foreign key fields.</p>
        </div>
        <div className="bsm-diagram-actions">
          <button
            className="bsm-icon-button"
            type="button"
            title="Copy DBML"
            aria-label="Copy database schema DBML"
            onClick={() => void copyText(tableSchemasToDbml(schemas))}
          >
            <i className="bi bi-copy" />
          </button>
          <button
            className="bsm-icon-button"
            type="button"
            title="Open fullscreen"
            aria-label="Open database schema fullscreen"
            onClick={() => setIsFullscreen(true)}
          >
            <i className="bi bi-arrows-fullscreen" />
          </button>
          <button
            className="bsm-icon-button"
            type="button"
            title="Save as PNG"
            aria-label="Save database schema as PNG"
            onClick={() =>
              void downloadDiagramPng(svg, "Database Schema Diagram")
            }
          >
            <i className="bi bi-download" />
          </button>
        </div>
      </header>
      <div
        className="bsm-schema-canvas"
        dangerouslySetInnerHTML={{ __html: svg }}
      />
      {isFullscreen && (
        <div
          className="bsm-fullscreen"
          role="dialog"
          aria-modal="true"
          aria-label="Database schema fullscreen"
        >
          <header>
            <h3>Database schema diagram</h3>
            <button
              className="bsm-icon-button"
              type="button"
              aria-label="Close fullscreen"
              onClick={() => setIsFullscreen(false)}
            >
              <i className="bi bi-x-lg" />
            </button>
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

function InfoMessage({ children }: { children: string }) {
  return (
    <div className="bsm-info-message">
      <i className="bi bi-info-circle" />
      <p>{children}</p>
    </div>
  );
}
