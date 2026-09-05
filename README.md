# WFHMonitor Developer

WFHMonitor has three separately running applications:

- `WFHMonitor/ClientApp`: React + TypeScript UI.
- `WFHMonitor.ApiService`: .NET 8 database, application, GitHub, and AI-proxy API.
- `WFHMonitor.AutomationService`: .NET 8 host for `codex app-server` and the shared ChatGPT OAuth account.

No OpenAI API key is used. ChatGPT credentials remain in Codex-managed
storage and are never returned to React, stored in WFHMonitor SQL, or placed
in application settings. The local shared secret below authenticates only
ApiService-to-AutomationService traffic.

## Configure local services

Choose one random local shared secret. Supply the same value to the two
.NET processes without committing it:

```powershell
# AutomationService terminal
$env:ServiceAuthentication__SharedSecret = "<your-local-random-secret>"

# ApiService terminal
$env:AiAutomationService__SharedSecret = "<the-same-local-random-secret>"
```

Editable non-secret settings are in:

- `WFHMonitor.AutomationService/appsettings.json`: Codex executable, model,
  reasoning effort, timeouts, prompt/schema directories, and workspace root.
- `WFHMonitor.ApiService/appsettings.json`: service address, Git executable,
  clone URL template, branch prefix, commit identity, workspace retention,
  GitHub OAuth, database, and other application settings.
- `WFHMonitor.AutomationService/Prompts` and
  `WFHMonitor.AutomationService/Schemas`: version-controlled AI instructions
  and strict JSON output schemas.

## Run

Open three PowerShell terminals at the repository root:

```powershell
dotnet run --project WFHMonitor.AutomationService
```

```powershell
dotnet run --project WFHMonitor.ApiService
```

```powershell
npm --prefix WFHMonitor/ClientApp run dev
```

Open the Vite URL, sign in as an administrator, open **Brainstorm**, and use
**Connect ChatGPT Plus**. The browser completes the official Codex OAuth
flow. Connect GitHub separately before using **Fix Bug**; the GitHub OAuth
scope must permit writes to the configured repository.

AI fixes are made in isolated retained workspaces with Codex network access
disabled. The API commits and pushes the changes with the requesting
administrator's GitHub connection and always creates a draft pull request.
Automation never merges it or marks it ready.
