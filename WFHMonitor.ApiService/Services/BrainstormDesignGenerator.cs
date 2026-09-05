using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public sealed class BrainstormDesignGenerator(
    IAiAutomationService automation,
    ILogger<BrainstormDesignGenerator> logger)
    : IBrainstormDesignGenerator
{
    public async Task<BrainstormDesignBlueprint> GenerateAsync(
        BrainstormGenerateDesignRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var blueprint = await automation.GenerateBrainstormAsync(
                request,
                cancellationToken);

            if (blueprint is null || blueprint.Diagrams.Count != 5)
            {
                logger.LogWarning(
                    "The Brainstorm AI response did not match the expected blueprint shape.");
                return BrainstormBlueprintConsistency.Apply(
                    BrainstormFallbackFactory.Create(
                        request,
                        "The AI response was incomplete. A rule-based blueprint was generated instead."),
                    request);
            }

            return BrainstormBlueprintConsistency.Apply(
                blueprint with
                {
                    SourceMode = "Codex",
                    Notice = null
                },
                request);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Codex Brainstorm generation failed. Returning a fallback blueprint.");
            return BrainstormBlueprintConsistency.Apply(
                BrainstormFallbackFactory.Create(
                    request,
                    "AI automation is disconnected or unavailable. A rule-based blueprint was generated so brainstorming can continue."),
                request);
        }
    }
}

internal static class BrainstormFallbackFactory
{
    public static BrainstormDesignBlueprint Create(
        BrainstormGenerateDesignRequest request,
        string notice)
    {
        var features = SplitFeatures(request.Features);
        var profile = DeploymentProfile.From(
            $"{request.Technology} {request.CloudHostingTarget}");
        var services = BuildServices(profile);
        var title = BuildTitle(request.Summary);

        return new(
            title,
            $"Use a modular web architecture at {ScaleLabel(request.UserCount)}: a React client, authenticated .NET 8 API, domain modules, transactional database, and queue-backed workers for slow or retryable work.",
            [
                $"React client for {string.Join(", ", features.Take(3))}",
                "ASP.NET Core .NET 8 API boundary",
                "Authentication and role-based authorization",
                "Domain services organized by business capability",
                "Relational database with indexed transactional tables",
                "Queue/cache and background worker for asynchronous tasks"
            ],
            services,
            $"Use {profile.Database} for transactional data and object storage for files. Begin with one write database, explicit indexes on ownership and status fields, backups, and short-lived caching for read-heavy views.",
            $"Expose versionable REST endpoints from ASP.NET Core .NET 8. Keep controllers focused on HTTP concerns, place rules in injected services, validate DTOs, enforce record ownership, and use workers for integrations and retries.",
            ScalingAdvice(request.UserCount),
            "Use HTTPS, secure server-side secrets, same-site authenticated sessions or OIDC, anti-forgery protection, input limits, per-record ownership checks, rate limits, audit logs, dependency patching, and tested restore procedures.",
            BuildCostEstimate(request.UserCount),
            [
                "A modular monolith is fast to build but module boundaries must be enforced to prevent coupling.",
                "A single relational database simplifies transactions but needs indexes, monitoring, and a scale-out plan.",
                "Real-time tracking, payments, and notifications add failure modes and should be isolated behind adapters and queues.",
                "Cloud estimates vary with traffic shape, retention, egress, and third-party usage."
            ],
            [
                "Build one complete user journey as a vertical slice.",
                "Confirm entities, ownership boundaries, and retention rules with stakeholders.",
                "Add authentication, authorization tests, and audit events.",
                "Instrument latency, errors, queue depth, and database load.",
                "Load-test the busiest read and write paths at the expected scale."
            ],
            BuildSchemas(features),
            [
                new("System Context", "context", ContextDiagram(title)),
                new("Architecture Components", "components",
                    ComponentsDiagram(profile)),
                new("Request and Data Flow", "dataFlow", DataFlowDiagram()),
                new("Detailed Architecture", "detailedArchitecture",
                    DetailedArchitectureDiagram(services)),
                new("Cloud Deployment Template", "cloudDeploymentTemplate",
                    CloudDeploymentDiagram(profile))
            ],
            "Fallback",
            notice,
            BuildServiceDetails(profile),
            BuildRestEndpoints(features),
            BuildGrpcContracts(),
            BuildBatchJobs());
    }

    private static IReadOnlyList<BrainstormDeploymentService> BuildServices(
        DeploymentProfile profile) =>
    [
        new("Customer / Admin Web Client", profile.Frontend,
            profile.FrontendRuntime,
            "Hosts the React bundle close to users and separates static delivery from API compute."),
        new("Authentication Layer", profile.Authentication,
            profile.AuthenticationRuntime,
            "Issues and validates identity before protected requests reach business modules."),
        new("API Gateway / Backend API", profile.Api, profile.ApiRuntime,
            "Provides the authenticated REST boundary, validation, routing, and authorization."),
        new("Domain Modules", profile.Domain, profile.DomainRuntime,
            "Runs product workflows in cohesive modules that can be extracted later if needed."),
        new("Background Worker", profile.Worker, profile.WorkerRuntime,
            "Processes notifications, reports, imports, retries, and integration callbacks."),
        new("Database", profile.Database, profile.DatabaseRuntime,
            "Stores transactional state with managed backups and restore support."),
        new("Cache / Queue", profile.CacheQueue, profile.CacheQueueRuntime,
            "Improves hot reads and decouples slow or retryable work from web requests.")
    ];

    private static IReadOnlyList<BrainstormServiceDetail> BuildServiceDetails(
        DeploymentProfile profile) =>
    [
        new(
            "Customer / Admin Web Client",
            "Client application",
            "Presents authenticated product and administration workflows.",
            profile.FrontendRuntime,
            profile.Frontend,
            "Owns no authoritative business data; keeps only transient UI state.",
            ["Authentication Layer", "Backend API"],
            ["HTTPS REST/JSON"]),
        new(
            "Authentication Layer",
            "Identity service",
            "Authenticates users and issues identity claims consumed by protected APIs.",
            profile.AuthenticationRuntime,
            profile.Authentication,
            "Owns identities, credentials, sessions, and authorization claims.",
            [],
            ["OIDC", "OAuth 2.0", "JWT or authenticated session"]),
        new(
            "Backend API",
            "API service",
            "Validates requests, enforces authorization, and coordinates domain use cases.",
            profile.ApiRuntime,
            profile.Api,
            "Owns public API contracts; domain modules own business records.",
            ["Authentication Layer", "Domain Modules", "Cache / Queue"],
            ["HTTPS REST/JSON", "In-process domain calls"]),
        new(
            "Domain Modules",
            "Modular domain service",
            "Implements business rules and transaction boundaries by capability.",
            profile.DomainRuntime,
            profile.Domain,
            "Each module owns its tables and publishes integration events instead of sharing writes.",
            ["Database", "Cache / Queue"],
            ["In-process calls initially", "gRPC if separately deployed", "Asynchronous events"]),
        new(
            "Background Worker",
            "Worker service",
            "Processes queued, scheduled, retryable, and long-running work outside HTTP requests.",
            profile.WorkerRuntime,
            profile.Worker,
            "Owns job execution state, attempts, checkpoints, and dead-letter metadata.",
            ["Cache / Queue", "Database"],
            ["Queue messages", "Optional internal gRPC"])
    ];

    private static IReadOnlyList<BrainstormRestEndpoint> BuildRestEndpoints(
        IReadOnlyList<string> features) =>
        features
            .Take(3)
            .SelectMany(feature =>
            {
                var resource = ToSnakeCase(feature).Replace('_', '-');
                var contract = ToPascalCase(feature);
                return new BrainstormRestEndpoint[]
                {
                    new(
                        "Backend API",
                        "GET",
                        $"/api/v1/{resource}",
                        $"Lists or searches {feature} records visible to the caller.",
                        "Authenticated user with record ownership or administrator role.",
                        "Query parameters for paging, filtering, and sorting.",
                        $"PagedResult<{contract}Response> JSON",
                        ["200", "400", "401", "403"]),
                    new(
                        "Backend API",
                        "POST",
                        $"/api/v1/{resource}",
                        $"Creates a new {feature} record through its domain workflow.",
                        "Authenticated user with create permission.",
                        $"Create{contract}Request JSON",
                        $"{contract}Response JSON with Location header",
                        ["201", "400", "401", "403", "409", "422"])
                };
            })
            .ToArray();

    private static IReadOnlyList<BrainstormGrpcContract> BuildGrpcContracts() =>
    [
        new(
            "Domain Modules",
            "domain.v1.DomainWorkflowService",
            "ExecuteWorkflow",
            "ExecuteWorkflowRequest",
            "ExecuteWorkflowResponse",
            "Unary",
            "Use this internal contract only after a domain module is separately deployed; prefer in-process calls in the initial modular architecture.")
    ];

    private static IReadOnlyList<BrainstormBatchJob> BuildBatchJobs() =>
    [
        new(
            "Integration outbox dispatcher",
            "Background Worker",
            "Timer and queue",
            "Every minute",
            "Publishes committed integration events and retries transient delivery failures.",
            "Unpublished outbox rows ordered by creation time.",
            "Published timestamps, attempt counts, and dead-letter records.",
            "Exponential backoff with jitter; dead-letter after the configured maximum attempts.",
            "Use the outbox event id as the downstream idempotency key."),
        new(
            "Daily operational summary",
            "Background Worker",
            "Cron",
            "0 2 * * * UTC",
            "Aggregates daily throughput, failures, and outstanding work for operational reporting.",
            "Previous UTC day's business events and job execution records.",
            "One versioned daily summary plus an audit event.",
            "Retry three times; retain the previous successful summary if all attempts fail.",
            "Upsert by summary date and schema version.")
    ];

    private static BrainstormCostEstimate BuildCostEstimate(string userCount)
    {
        var count = ParseUserCount(userCount);
        return count switch
        {
            < 1_000 => Cost(
                "$20 - $120 / month",
                "Small prototype using one API instance, a small database, and low-volume telemetry.",
                "$5 - $35", "$5 - $45", "$0 - $15", "$5 - $25"),
            < 100_000 => Cost(
                "$150 - $900 / month",
                "Growing product with redundant API compute, managed SQL, cache/queue, backups, and monitoring.",
                "$50 - $300", "$50 - $340", "$20 - $130", "$30 - $130"),
            < 1_000_000 => Cost(
                "$1,000 - $7,000 / month",
                "High traffic with load-balanced compute, larger SQL capacity, CDN, workers, cache, queue, and observability.",
                "$300 - $2,000", "$400 - $2,800", "$100 - $900",
                "$200 - $1,000"),
            >= 1_000_000 => Cost(
                "$8,000+ / month",
                "Large sustained scale requiring capacity planning, partitioning, disaster recovery, and multiple compute pools.",
                "$2,500+", "$3,000+", "$1,000+", "$1,500+"),
            _ => Cost(
                "$50 - $300 / month",
                "Traffic is unclear, so begin with a small managed stack and measure before scaling.",
                "$20 - $90", "$15 - $90", "$0 - $50", "$10 - $70")
        };
    }

    private static BrainstormCostEstimate Cost(
        string range,
        string summary,
        string compute,
        string database,
        string queue,
        string telemetry) =>
        new(
            "USD",
            range,
            summary,
            [
                new("Compute / hosting", compute,
                    "React hosting, API compute, workers, and load balancing where needed."),
                new("Database / storage", database,
                    "Relational capacity, backups, and object storage."),
                new("Cache / queue", queue,
                    "Managed cache and asynchronous messaging."),
                new("Monitoring / logs", telemetry,
                    "Metrics, traces, error reporting, logs, and alerts.")
            ],
            [
                "One primary region and normal CRUD traffic.",
                "A light staging environment is included only at growing scale.",
                "Developer time and third-party transaction, email, SMS, and AI fees are excluded."
            ],
            [
                "Start with the smallest reliable managed tiers.",
                "Cache only measured hot paths and set log-retention budgets.",
                "Scale workers independently and use CDN delivery for static/public assets."
            ]);

    private static IReadOnlyList<BrainstormTableSchema> BuildSchemas(
        IReadOnlyList<string> features)
    {
        var featureColumns = features
            .Take(3)
            .Select(feature => new BrainstormTableColumn(
                $"{ToSnakeCase(feature)}_enabled",
                "boolean",
                false,
                false,
                $"Status or feature marker for {feature}."))
            .ToArray();

        return
        [
            new(
                "users",
                "User identity and profile basics.",
                [
                    new("id", "uuid", true, false, "Unique user identifier."),
                    new("email", "varchar(255)", false, false,
                        "Unique normalized login/contact address."),
                    new("display_name", "varchar(160)", false, false,
                        "Name displayed in the client."),
                    new("role", "varchar(40)", false, false,
                        "Coarse authorization role."),
                    new("created_at", "datetime", false, false,
                        "UTC creation timestamp.")
                ],
                ["Referenced by workspaces and audit events."]),
            new(
                "workspaces",
                "Top-level owned business workspace.",
                [
                    new("id", "uuid", true, false,
                        "Unique workspace identifier."),
                    new("owner_user_id", "uuid", false, true,
                        "References users.id."),
                    new("name", "varchar(180)", false, false,
                        "Human-readable workspace name."),
                    new("status", "varchar(40)", false, false,
                        "Draft, active, or archived."),
                    new("created_at", "datetime", false, false,
                        "UTC creation timestamp.")
                ],
                ["owner_user_id -> users.id"]),
            new(
                "feature_records",
                "Feature-specific state attached to a workspace.",
                [
                    new("id", "uuid", true, false, "Unique record identifier."),
                    new("workspace_id", "uuid", false, true,
                        "References workspaces.id."),
                    .. featureColumns,
                    new("updated_at", "datetime", false, false,
                        "UTC last-change timestamp.")
                ],
                ["workspace_id -> workspaces.id"]),
            new(
                "audit_events",
                "Security and business audit trail.",
                [
                    new("id", "uuid", true, false, "Unique event identifier."),
                    new("actor_user_id", "uuid", false, true,
                        "References users.id."),
                    new("workspace_id", "uuid", false, true,
                        "References workspaces.id when applicable."),
                    new("event_type", "varchar(100)", false, false,
                        "Stable event name."),
                    new("payload_json", "text", false, false,
                        "Small metadata payload without secrets."),
                    new("created_at", "datetime", false, false,
                        "UTC event timestamp.")
                ],
                [
                    "actor_user_id -> users.id",
                    "workspace_id -> workspaces.id"
                ])
        ];
    }

    private static string ContextDiagram(string title) =>
        $$"""
        flowchart LR
          Customer[Customer] --> App["{{EscapeMermaid(title)}}"]
          Operator[Admin / Operator] --> App
          App --> Identity[Identity Provider]
          App --> External[External Services]
          App --> Notify[Email / Notifications]
        """;

    private static string ComponentsDiagram(DeploymentProfile profile) =>
        $$"""
        flowchart TB
          Browser["React Client<br/>{{EscapeMermaid(profile.Frontend)}}"] --> Api[".NET 8 API<br/>{{EscapeMermaid(profile.Api)}}"]
          Api --> Identity["Authentication<br/>{{EscapeMermaid(profile.Authentication)}}"]
          Api --> Domain["Domain Modules<br/>{{EscapeMermaid(profile.Domain)}}"]
          Domain --> Db[("{{EscapeMermaid(profile.Database)}}")]
          Domain --> Queue["{{EscapeMermaid(profile.CacheQueue)}}"]
          Queue --> Worker["Worker<br/>{{EscapeMermaid(profile.Worker)}}"]
          Worker --> Db
        """;

    private static string DataFlowDiagram() =>
        """
        sequenceDiagram
          actor User
          participant UI as React UI
          participant API as .NET 8 API
          participant Domain as Domain Service
          participant DB as SQL Database
          participant Queue as Queue / Worker
          User->>UI: Submit action
          UI->>API: HTTPS + authenticated session
          API->>Domain: Validate and authorize use case
          Domain->>DB: Read or write transaction
          Domain-->>Queue: Publish slow work
          DB-->>Domain: Persisted result
          Domain-->>API: Response DTO
          API-->>UI: JSON response
          UI-->>User: Updated workspace
        """;

    private static string DetailedArchitectureDiagram(
        IReadOnlyList<BrainstormDeploymentService> services)
    {
        var service = (string module) =>
            services.First(item => item.Module == module);
        var auth = service("Authentication Layer");
        var api = service("API Gateway / Backend API");
        var domain = service("Domain Modules");
        var database = service("Database");
        var worker = service("Background Worker");
        var queue = service("Cache / Queue");
        return $$"""
        flowchart LR
          User[Web User] --> Auth{"{{EscapeMermaid(auth.RecommendedService)}}"}
          Mobile[Mobile Client] --> Auth
          Partner[Partner Service] --> Auth
          Auth -->|Verified| Api["API Boundary<br/>{{EscapeMermaid(api.RecommendedService)}}"]
          Auth -.->|Rejected| Rejected((Rejected))
          subgraph Modules["INTERNAL MODULES"]
            Api --> Core["Core Workflows<br/>{{EscapeMermaid(domain.RecommendedService)}}"]
            Api --> Admin["Administration<br/>{{EscapeMermaid(domain.Runtime)}}"]
            Core --> Db[("{{EscapeMermaid(database.RecommendedService)}}")]
            Core --> Queue["{{EscapeMermaid(queue.RecommendedService)}}"]
            Queue --> Worker["{{EscapeMermaid(worker.RecommendedService)}}"]
            Worker --> Db
          end
          subgraph Legend["LEGEND"]
            L1[Solid = accepted request/data flow]
            L2[Dashed = rejected request]
            L3[Second line = deployment service/runtime]
          end
        """;
    }

    private static string CloudDeploymentDiagram(DeploymentProfile profile) =>
        $$"""
        flowchart LR
          subgraph Cloud["Cloud / Hosting Environment"]
            subgraph Product["Customer / Product Account"]
              User([User]) --> N1((1)) --> UI["{{EscapeMermaid(profile.Frontend)}}"]
              UI --> N2((2)) --> Auth["{{EscapeMermaid(profile.Authentication)}}"]
              UI --> N3((3)) --> API["{{EscapeMermaid(profile.Api)}}"]
              API --> N4((4)) --> Domain["{{EscapeMermaid(profile.Domain)}}"]
              Domain --> N5((5)) --> DB[("{{EscapeMermaid(profile.Database)}}")]
              Domain --> Queue["{{EscapeMermaid(profile.CacheQueue)}}"]
              Queue --> N6((6)) --> Worker["{{EscapeMermaid(profile.Worker)}}"]
            end
            subgraph Shared["Managed / Shared Services"]
              CDN[CDN / Static Assets]
              Registry[Artifact / Container Registry]
              Secrets[Secrets]
              Logs[Logs / Metrics / Alerts]
              Backup[Backups]
            end
            CDN --> UI
            Registry --> Worker
            API -.-> Logs
            Domain -.-> Secrets
            DB -.-> Backup
          end
        """;

    private static IReadOnlyList<string> SplitFeatures(string value) =>
        value.Split(
                [',', ';', '\n', '\r'],
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Where(feature => feature.Length > 0)
            .Take(6)
            .DefaultIfEmpty("Core product workflow")
            .ToArray();

    private static string BuildTitle(string summary)
    {
        var cleaned = Regex.Replace(summary, @"\s+", " ").Trim();
        return cleaned.Length <= 70 ? cleaned : $"{cleaned[..67]}...";
    }

    private static string ScaleLabel(string userCount)
    {
        var count = ParseUserCount(userCount);
        return count switch
        {
            < 1_000 => "early prototype scale",
            < 100_000 => "growing-product scale",
            < 1_000_000 => "high-traffic scale",
            >= 1_000_000 => "large sustained scale",
            _ => $"the stated target ({userCount})"
        };
    }

    private static string ScalingAdvice(string userCount)
    {
        var count = ParseUserCount(userCount);
        return count switch
        {
            < 1_000 =>
                "Start with one API deployment and managed SQL. Measure latency and database load before introducing cache or more services.",
            < 100_000 =>
                "Run redundant stateless API instances, managed SQL with tuned indexes, queue-backed workers, selective cache, CDN delivery, and baseline observability.",
            < 1_000_000 =>
                "Add autoscaling, capacity tests, read replicas where justified, CDN and cache strategy, queue partitioning, back-pressure, and tested recovery.",
            >= 1_000_000 =>
                "Partition high-volume data and workloads, isolate independently scaled capabilities, use regional failover, control cache invalidation, and rehearse disaster recovery.",
            _ =>
                "Start simple, instrument real traffic, define service-level objectives, and scale compute, SQL, cache, and workers from measured bottlenecks."
        };
    }

    private static int? ParseUserCount(string userCount)
    {
        var normalized = userCount
            .ToLowerInvariant()
            .Replace(",", string.Empty, StringComparison.Ordinal);
        var match = Regex.Match(
            normalized,
            @"(\d+)(?:\.(\d+))?\s*([kmb])?");
        if (!match.Success) return null;

        if (!decimal.TryParse(
                match.Groups[1].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number))
            return null;
        if (match.Groups[2].Success
            && decimal.TryParse(
                $"0.{match.Groups[2].Value}",
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var decimalPart))
            number += decimalPart;

        var multiplier = match.Groups[3].Value switch
        {
            "k" => 1_000m,
            "m" => 1_000_000m,
            "b" => 1_000_000_000m,
            _ => 1m
        };
        return (int)Math.Min(int.MaxValue, number * multiplier);
    }

    private static string EscapeMermaid(string value) =>
        value.Replace("\"", "'", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private static string ToSnakeCase(string value)
    {
        var cleaned = Regex.Replace(
                value.Trim().ToLowerInvariant(),
                @"[^a-z0-9]+",
                "_")
            .Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "feature" : cleaned;
    }

    private static string ToPascalCase(string value)
    {
        var words = Regex.Split(value, @"[^a-zA-Z0-9]+")
            .Where(word => word.Length > 0)
            .Select(word =>
                char.ToUpperInvariant(word[0])
                + word[1..].ToLowerInvariant());
        var cleaned = string.Concat(words);
        return string.IsNullOrWhiteSpace(cleaned) ? "Feature" : cleaned;
    }

    private sealed record DeploymentProfile(
        string Frontend,
        string FrontendRuntime,
        string Authentication,
        string AuthenticationRuntime,
        string Api,
        string ApiRuntime,
        string Domain,
        string DomainRuntime,
        string Worker,
        string WorkerRuntime,
        string Database,
        string DatabaseRuntime,
        string CacheQueue,
        string CacheQueueRuntime)
    {
        public static DeploymentProfile From(string technology)
        {
            var value = technology.ToLowerInvariant();
            if (value.Contains("aws", StringComparison.Ordinal)
                || value.Contains("fargate", StringComparison.Ordinal)
                || value.Contains("lambda", StringComparison.Ordinal))
            {
                return new(
                    "S3 + CloudFront", "Static React hosting",
                    "Amazon Cognito", "OIDC / JWT identity provider",
                    "API Gateway + ECS/Fargate", "Containerized .NET 8 API",
                    "ECS/Fargate Services", "Containerized domain modules",
                    "Lambda or ECS Worker", "Queue-triggered background compute",
                    value.Contains("dynamo", StringComparison.Ordinal)
                        ? "DynamoDB"
                        : "Amazon RDS",
                    "Managed transactional database",
                    "SQS + ElastiCache Redis", "Managed queue and cache");
            }

            if (value.Contains("azure", StringComparison.Ordinal))
            {
                return new(
                    "Azure Static Web Apps / Front Door", "Static React hosting",
                    "Microsoft Entra External ID", "OIDC / JWT identity provider",
                    "Azure App Service", "ASP.NET Core .NET 8 API",
                    "Azure App Service Modules", "Modular .NET service layer",
                    "Azure Functions / WebJob", "Queue-triggered worker",
                    "Azure SQL Database", "Managed SQL database",
                    "Azure Service Bus + Redis", "Managed queue and cache");
            }

            if (value.Contains("iis", StringComparison.Ordinal)
                || value.Contains("windows", StringComparison.Ordinal)
                || value.Contains("sql server", StringComparison.Ordinal))
            {
                return new(
                    "IIS Static Site", "React production build",
                    "ASP.NET Core Identity", "Cookie / OIDC authentication",
                    "IIS + ASP.NET Core API", "Kestrel behind IIS",
                    "ASP.NET Core Modules", "Modular .NET 8 service layer",
                    "Windows Service / Hangfire", "Background worker process",
                    "SQL Server", "Relational database",
                    "Redis + durable job queue", "Cache and asynchronous jobs");
            }

            return new(
                "Static Web Hosting", "React production build",
                "OIDC Provider / ASP.NET Core Identity", "OIDC / JWT or cookie auth",
                "ASP.NET Core API", ".NET 8 on Kestrel",
                "ASP.NET Core Domain Modules", "Modular monolith",
                "Hosted Worker Service", ".NET 8 background process",
                "PostgreSQL or SQL Server", "Managed relational database",
                "Redis + durable queue", "Cache and asynchronous messaging");
        }
    }
}

public static class BrainstormBlueprintSchema
{
    public static readonly JsonNode Value = JsonNode.Parse(
        """
        {
          "type": "object",
          "additionalProperties": false,
          "required": [
            "title", "recommendedArchitecture", "mainComponents",
            "deploymentServices", "databaseStorageRecommendation",
            "apiBackendRecommendation", "scalingAdvice", "securityNotes",
            "costEstimate", "risksTradeoffs", "nextSteps", "tableSchemas",
            "diagrams", "sourceMode", "notice", "serviceDetails",
            "restEndpoints", "grpcContracts", "batchJobs"
          ],
          "properties": {
            "title": { "type": "string", "minLength": 1 },
            "recommendedArchitecture": { "type": "string", "minLength": 1 },
            "mainComponents": {
              "type": "array",
              "items": { "type": "string", "minLength": 1 }
            },
            "deploymentServices": {
              "type": "array",
              "minItems": 4,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["module", "recommendedService", "runtime", "reason"],
                "properties": {
                  "module": { "type": "string", "minLength": 1 },
                  "recommendedService": { "type": "string", "minLength": 1 },
                  "runtime": { "type": "string", "minLength": 1 },
                  "reason": { "type": "string", "minLength": 1 }
                }
              }
            },
            "serviceDetails": {
              "type": "array",
              "minItems": 2,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": [
                  "name", "type", "responsibility", "runtime", "deployment",
                  "dataOwnership", "dependencies", "communication"
                ],
                "properties": {
                  "name": { "type": "string", "minLength": 1 },
                  "type": { "type": "string", "minLength": 1 },
                  "responsibility": { "type": "string", "minLength": 1 },
                  "runtime": { "type": "string", "minLength": 1 },
                  "deployment": { "type": "string", "minLength": 1 },
                  "dataOwnership": { "type": "string", "minLength": 1 },
                  "dependencies": {
                    "type": "array",
                    "items": { "type": "string", "minLength": 1 }
                  },
                  "communication": {
                    "type": "array",
                    "items": { "type": "string", "minLength": 1 }
                  }
                }
              }
            },
            "restEndpoints": {
              "type": "array",
              "minItems": 1,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": [
                  "service", "method", "path", "purpose", "authentication",
                  "request", "response", "statusCodes"
                ],
                "properties": {
                  "service": { "type": "string", "minLength": 1 },
                  "method": {
                    "type": "string",
                    "enum": ["GET", "POST", "PUT", "PATCH", "DELETE"]
                  },
                  "path": { "type": "string", "minLength": 1 },
                  "purpose": { "type": "string", "minLength": 1 },
                  "authentication": { "type": "string", "minLength": 1 },
                  "request": { "type": "string", "minLength": 1 },
                  "response": { "type": "string", "minLength": 1 },
                  "statusCodes": {
                    "type": "array",
                    "items": { "type": "string", "minLength": 1 }
                  }
                }
              }
            },
            "grpcContracts": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": [
                  "service", "contract", "rpcMethod", "requestMessage",
                  "responseMessage", "streaming", "purpose"
                ],
                "properties": {
                  "service": { "type": "string", "minLength": 1 },
                  "contract": { "type": "string", "minLength": 1 },
                  "rpcMethod": { "type": "string", "minLength": 1 },
                  "requestMessage": { "type": "string", "minLength": 1 },
                  "responseMessage": { "type": "string", "minLength": 1 },
                  "streaming": {
                    "type": "string",
                    "enum": [
                      "Unary", "Client streaming", "Server streaming",
                      "Bidirectional streaming"
                    ]
                  },
                  "purpose": { "type": "string", "minLength": 1 }
                }
              }
            },
            "batchJobs": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": [
                  "name", "ownerService", "trigger", "schedule",
                  "responsibility", "input", "output", "retryPolicy",
                  "idempotencyStrategy"
                ],
                "properties": {
                  "name": { "type": "string", "minLength": 1 },
                  "ownerService": { "type": "string", "minLength": 1 },
                  "trigger": { "type": "string", "minLength": 1 },
                  "schedule": { "type": "string", "minLength": 1 },
                  "responsibility": { "type": "string", "minLength": 1 },
                  "input": { "type": "string", "minLength": 1 },
                  "output": { "type": "string", "minLength": 1 },
                  "retryPolicy": { "type": "string", "minLength": 1 },
                  "idempotencyStrategy": { "type": "string", "minLength": 1 }
                }
              }
            },
            "databaseStorageRecommendation": { "type": "string", "minLength": 1 },
            "apiBackendRecommendation": { "type": "string", "minLength": 1 },
            "scalingAdvice": { "type": "string", "minLength": 1 },
            "securityNotes": { "type": "string", "minLength": 1 },
            "costEstimate": {
              "type": "object",
              "additionalProperties": false,
              "required": [
                "currency", "monthlyRange", "summary", "lineItems",
                "assumptions", "costOptimizations"
              ],
              "properties": {
                "currency": { "type": "string", "minLength": 1 },
                "monthlyRange": { "type": "string", "minLength": 1 },
                "summary": { "type": "string", "minLength": 1 },
                "lineItems": {
                  "type": "array",
                  "minItems": 1,
                  "items": {
                    "type": "object",
                    "additionalProperties": false,
                    "required": ["name", "monthlyRange", "notes"],
                    "properties": {
                      "name": { "type": "string", "minLength": 1 },
                      "monthlyRange": { "type": "string", "minLength": 1 },
                      "notes": { "type": "string", "minLength": 1 }
                    }
                  }
                },
                "assumptions": {
                  "type": "array",
                  "items": { "type": "string", "minLength": 1 }
                },
                "costOptimizations": {
                  "type": "array",
                  "items": { "type": "string", "minLength": 1 }
                }
              }
            },
            "risksTradeoffs": {
              "type": "array",
              "items": { "type": "string", "minLength": 1 }
            },
            "nextSteps": {
              "type": "array",
              "items": { "type": "string", "minLength": 1 }
            },
            "tableSchemas": {
              "type": "array",
              "minItems": 3,
              "maxItems": 6,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["name", "purpose", "columns", "relationships"],
                "properties": {
                  "name": { "type": "string", "minLength": 1 },
                  "purpose": { "type": "string", "minLength": 1 },
                  "columns": {
                    "type": "array",
                    "minItems": 3,
                    "items": {
                      "type": "object",
                      "additionalProperties": false,
                      "required": [
                        "name", "type", "isPrimaryKey", "isForeignKey", "notes"
                      ],
                      "properties": {
                        "name": { "type": "string", "minLength": 1 },
                        "type": { "type": "string", "minLength": 1 },
                        "isPrimaryKey": { "type": "boolean" },
                        "isForeignKey": { "type": "boolean" },
                        "notes": { "type": "string", "minLength": 1 }
                      }
                    }
                  },
                  "relationships": {
                    "type": "array",
                    "items": { "type": "string", "minLength": 1 }
                  }
                }
              }
            },
            "diagrams": {
              "type": "array",
              "minItems": 5,
              "maxItems": 5,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["title", "kind", "mermaid"],
                "properties": {
                  "title": { "type": "string", "minLength": 1 },
                  "kind": {
                    "type": "string",
                    "enum": [
                      "context", "components", "dataFlow",
                      "detailedArchitecture", "cloudDeploymentTemplate"
                    ]
                  },
                  "mermaid": { "type": "string", "minLength": 1 }
                }
              }
            },
            "sourceMode": { "type": "string", "minLength": 1 },
            "notice": { "type": ["string", "null"] }
          }
        }
        """)
        ?? throw new InvalidOperationException(
            "The Brainstorm blueprint JSON schema is invalid.");
}
