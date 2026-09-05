import { useState } from "react";
import { MetricGrid, PageHeader } from "../../components/DataDisplay";
import { PageBoundary } from "../../components/PageBoundary";
import { Notice } from "../../components/PageStates";
import { ServerAction } from "../../components/ServerAction";
import { usePage } from "../../hooks/usePage";
import { apiRoutes } from "../../services/microservices";

interface SubscriptionModel {
  currentPlan: string;
  isProSubscriptionActive: boolean;
  proSubscriptionEndsAt?: string | null;
  isProCancelAtPeriodEnd: boolean;
  isStripeBillingConfigured: boolean;
  hasPendingStripeCheckout: boolean;
  pendingStripeCheckoutUrl?: string | null;
  currentProjectCount: number;
  currentBugCount: number;
  currentFeatureCount: number;
  freeProjectLimit: number;
  freeBugLimit: number;
  freeFeatureLimit: number;
  allowAiAutomationForFreePlan: boolean;
  hasProAccess: boolean;
  requiresProPayment: boolean;
  canStartProCheckout: boolean;
  canCancelProPlan: boolean;
}

export function SubscriptionPage() {
  const page = usePage<SubscriptionModel>(apiRoutes.payments.index);
  const [message, setMessage] = useState<string | null>(null);

  return (
    <PageBoundary {...page}>
      {page.data && (
        <>
          {message && (
            <Notice onDismiss={() => setMessage(null)}>{message}</Notice>
          )}
          <PageHeader
            eyebrow="Billing"
            title="Subscription"
            description="Choose limits and automation access that fit the team."
          />
          <section className="plan-layout">
            <article
              className={`surface plan-card ${!page.data.hasProAccess ? "selected" : ""}`}
            >
              <span className="eyebrow">Current basics</span>
              <h2>Free</h2>
              <strong>
                $0 <small>/ month</small>
              </strong>
              <ul>
                <li>{page.data.freeProjectLimit} projects</li>
                <li>{page.data.freeBugLimit} bugs</li>
                <li>{page.data.freeFeatureLimit} features</li>
                <li>
                  {page.data.allowAiAutomationForFreePlan
                    ? "AI automation included"
                    : "Manual workflows"}
                </li>
              </ul>
              {!page.data.hasProAccess && (
                <span className="current-plan">Current plan</span>
              )}
            </article>
            <article
              className={`surface plan-card pro ${page.data.hasProAccess ? "selected" : ""}`}
            >
              <span className="eyebrow">For active delivery teams</span>
              <h2>Pro</h2>
              <strong>
                $29 <small>/ month</small>
              </strong>
              <ul>
                <li>Unlimited tracked work</li>
                <li>Codex AI automation</li>
                <li>Cloud monitoring</li>
                <li>Project health analysis</li>
              </ul>
              {page.data.hasProAccess ? (
                <>
                  <span className="current-plan">Pro active</span>
                  {page.data.canCancelProPlan && (
                    <ServerAction
                      endpoint={apiRoutes.payments.cancelPlan}
                      confirmMessage="Cancel Pro at the end of the billing period?"
                      onComplete={(response) => {
                        setMessage(
                          response.success ?? "Subscription updated."
                        );
                        void page.reload();
                      }}
                    >
                      Cancel renewal
                    </ServerAction>
                  )}
                </>
              ) : (
                <ServerAction
                  endpoint={apiRoutes.payments.startProCheckout}
                  values={{ returnUrl: "/app/subscription" }}
                  className="button button-primary"
                  onComplete={(response) =>
                    setMessage(response.success ?? "Checkout started.")}
                >
                  Continue to checkout
                </ServerAction>
              )}
            </article>
          </section>
          <section className="surface usage-panel">
            <div className="section-heading">
              <div>
                <span className="eyebrow">Usage</span>
                <h2>Current workspace</h2>
              </div>
            </div>
            <MetricGrid
              metrics={[
                {
                  label: "Projects",
                  value: page.data.currentProjectCount,
                  hint: `${page.data.freeProjectLimit} Free limit`,
                  icon: "bi-kanban"
                },
                {
                  label: "Bugs",
                  value: page.data.currentBugCount,
                  hint: `${page.data.freeBugLimit} Free limit`,
                  icon: "bi-bug"
                },
                {
                  label: "Features",
                  value: page.data.currentFeatureCount,
                  hint: `${page.data.freeFeatureLimit} Free limit`,
                  icon: "bi-list-check"
                }
              ]}
            />
          </section>
        </>
      )}
    </PageBoundary>
  );
}
