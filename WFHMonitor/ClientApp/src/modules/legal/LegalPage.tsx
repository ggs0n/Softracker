import { PageHeader } from "../../components/DataDisplay";

export function LegalPage({ kind }: { kind: "privacy" | "terms" }) {
  const privacy = kind === "privacy";

  return (
    <>
      <PageHeader
        eyebrow="Legal"
        title={privacy ? "Privacy policy" : "Terms of service"}
        description="Last updated 29 July 2026"
      />
      <article className="surface legal-copy">
        <h2>
          {privacy ? "How workspace data is handled" : "Using Softracker"}
        </h2>
        <p>
          {privacy
            ? "Softracker processes account, project, quality, and activity data to provide the workspace features your organization enables. Your organization controls its workspace data and connected integrations."
            : "Use Softracker only for work you are authorized to access. Workspace administrators control membership, roles, connected services, and subscription settings."}
        </p>
        <p>
          {privacy
            ? "Connected providers receive only the requests needed for their configured features. Contact your workspace administrator for access, correction, retention, or deletion requests."
            : "Do not upload unlawful content, expose another organization’s data, or interfere with the service. Subscription and provider charges follow the plan shown before checkout."}
        </p>
      </article>
    </>
  );
}
