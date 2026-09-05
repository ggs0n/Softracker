# WFHMonitor

WFHMonitor, presented in the UI as Softracker, is an ASP.NET Core MVC application for managing projects, features, bugs, QA activity, teams, subscriptions, and developer work.

## Main features

- Project and feature tracking
- Bug reporting, assignment, severity, evidence, and activity history
- QA test cases and module-based scanning
- Team, role, permission, notification, and onboarding management
- GitHub repository and OAuth integration
- Codex-assisted code scanning, bug analysis, fix planning, and feature guidance
- Stripe subscription support and application health checks

## Technology

The solution uses .NET 8, ASP.NET Core MVC, Entity Framework Core, ASP.NET Core Identity, SQL Server, Bootstrap, and xUnit.

## Prerequisites

Install the .NET 8 SDK and SQL Server LocalDB or another compatible SQL Server instance.
Install the Codex CLI only if you want to use the AI-assisted scanning features.

## Configuration

Copy `WFHMonitor/appsettings.example.json` to `WFHMonitor/appsettings.json`, then update the database connection and any optional GitHub, Stripe, or Codex settings.
Keep credentials in the ignored local settings files or environment variables and never commit real secrets.

## Run locally

```powershell
dotnet restore WFHMonitor.slnx
dotnet run --project WFHMonitor/WFHMonitor.csproj --launch-profile http
```

The application starts at `http://localhost:5227/Auth/Login`, and startup automatically applies the required database migrations and compatibility updates.

## Connect Codex

Run `codex login` and complete the ChatGPT browser sign-in, or use the admin-only **Connect Codex** button in the application.
Background scanning and implementation jobs use the authenticated Codex CLI through `codex exec`.

## Tests

Run the automated tests with the following command.

```powershell
dotnet test WFHMonitor.Tests/WFHMonitor.Tests.csproj
```

The `/health` endpoint can be used to verify that the application and database are available.
