# SprintPilot

A local Azure DevOps sprint-management companion built with .NET 10, ASP.NET Core and Blazor Interactive Server. Azure DevOps is the source of truth. No Azure hosting, database, AI subscription or AI API key is required.

**Delivery status:** implementation supplied for local evaluation. Release build and automated application/HTTP-adapter checks pass. A real Azure DevOps tenant, Windows Credential Manager, Windows launch scripts and browser interactions still require the AVD acceptance checks in `docs/AVD-ACCEPTANCE.md`. Do not treat performance targets as measured results.

## Run from Visual Studio (recommended)

1. Open `SprintPilot.sln` in Visual Studio with .NET 10 support and the ASP.NET/web development workload.
2. Set **SprintPilot.Web** as the startup project.
3. Select the **SprintPilot** launch profile (Project, not IIS Express).
4. Press **F5**. The app opens the browser after successfully binding to localhost:5271, using its private launch session automatically.
5. Use **Shift+F5** to stop debugging and release the port.

No install/publish script or separately launched EXE is required. Close any previously running published instance first; Visual Studio cannot take a port held by another process. The profile keeps session protection enabled and does not put the PAT in launch settings.

## Double-click installation and launch

With the .NET 10 SDK installed, double-click **Install-SprintPilot.cmd** in the repository root. Setup builds the app, checks it, creates a desktop shortcut, and opens the browser. Updates safely stop only the verified instance belonging to this checkout before publishing.

For daily use, double-click **Open-SprintPilot.cmd** or the desktop shortcut. It reuses a running instance. **Stop-SprintPilot.cmd** stops the verified instance. Do not launch the published EXE directly.

These entry points use your normal Windows account and respect AVD PowerShell policy. Errors stay visible. The permissions fix writes only the app folder's access rules; it does not request audit privileges or change ownership.

## Start on your Windows AVD

1. Clone `https://github.com/vdskevin009/sprintpilot.git` onto your AVD, or extract the source ZIP locally. Keep the checkout in a location you can write to.
2. Open PowerShell in the repository root.
3. Run:

```powershell
.\scripts\Setup-SprintPilot.ps1
```

Setup checks for the .NET 10 SDK, restores, builds, runs the two test executables, publishes a Release build, saves the port, creates a desktop shortcut where permitted, and opens SprintPilot. It does not provision Azure resources or require administrator privileges. The initial setup takes longer than ordinary launches.

4. Enter the organization **name**, project, and PAT on the welcome screen. Test the connection, then save locally. Select your team in Settings if needed. Team iterations and area paths must already be configured in Azure DevOps.
5. For future use, click the desktop shortcut or run:

```powershell
.\scripts\Start-SprintPilot.ps1
```

The launcher opens `http://localhost:5271`. A private per-process launch capability unlocks the browser session. Opening that URL directly in a fresh browser shows an instruction to use the shortcut; this is intentional on shared AVD machines. Launch capabilities never contain the Azure DevOps PAT.

To stop or update:

```powershell
.\scripts\Stop-SprintPilot.ps1
# After pulling source updates:
.\scripts\Setup-SprintPilot.ps1
```

Use `-Port 5272` on Setup for a different stable port. Start and Stop then use the saved port; both also accept an explicit `-Port`. Use `Setup-SprintPilot.ps1 -NoShortcut -NoLaunch` for setup without opening Edge. Start accepts `-NoBrowser`.

**Prerequisites:** Windows PowerShell 5.1+ or PowerShell 7; .NET 10 SDK; Edge; Git when cloning; Azure DevOps Services access. Installation of the SDK and execution of PowerShell scripts are subject to your AVD organization's policies. If scripts are blocked, use your approved script-signing/execution process; these scripts do not bypass execution policies.

## Credentials and local data

The preferred Windows path is the first-run form. The PAT is saved as a generic Windows Credential Manager entry named `SprintPilot:<organization>:<project>`, under the current Windows user. The browser receives no stored PAT. The password field does not render a server-supplied value.

Environment variables are also supported:

```text
SPRINTPILOT_AZDO_ORGANIZATION
SPRINTPILOT_AZDO_PROJECT
SPRINTPILOT_AZDO_PAT
SPRINTPILOT_PORT          (optional)
```

Supply them through your approved process environment/secret tooling. Avoid typing token-bearing commands into shell history. `.env.example` documents the names; **the app does not read `.env` automatically**. Environment credentials take precedence on new sessions. Saving in Settings updates the current circuit and stored Windows credentials; remove environment overrides if you want subsequent sessions to use local configuration.

Use a PAT scoped to the chosen organization with Work Items read/write and Project and Team read permissions. Existing project, team and work-item permissions still apply. Additional organization policies may restrict PAT use. Expired or insufficient tokens produce a safe error. Authentication is behind `ITrackerAuthentication` and `ICredentialStore` for future Entra ID support; Entra login is not implemented in this version.

Non-secret connection metadata, preferences, templates and launcher settings are under `%LOCALAPPDATA%\SprintPilot`, outside the checkout. Setup/Start apply a user-and-SYSTEM-only ACL to that directory. Session launch files contain a random local capability, not a PAT. Do not share them. On non-Windows systems, UI-supplied credentials last only for the current circuit; there is no plaintext persistence fallback.

The server binds only to loopback, rejects unexpected Host and Origin headers, and requires a private HttpOnly SameSite session cookie for application and Blazor endpoints. The launch key travels in the URL fragment and is immediately removed by the launch page; it is not included in server URLs or logs. Run a separate instance/port for each AVD user. This protects against other unprivileged local users and unrelated websites; it does not protect against an administrator or malicious software running as the same Windows user.

Azure API redirects are disabled. Request bodies, credentials, Azure error bodies and work-item content are not logged. Structured console logs contain framework lifecycle events and Azure status codes. There is no persistent file logger in this version. Raw Azure HTML is displayed as encoded plain text; imported text is HTML-encoded before writing to Azure DevOps.

## Planning dashboard

The new **Planning overview** page compares application and initiative tags across current sprint, next sprint, next week, backlog and total. It also shows each person's current/next workload against configurable per-sprint targets, initially 50–60 hours. Classify tags and confirm estimate units in Planning settings. See [the planning guide](docs/PLANNING.md) for scope, overlap arithmetic and capacity definitions.

## Daily workflows

- **Sprint workspace:** previous/current/next iteration controls; searchable iteration picker; ID/title/tag search; owner, state, type, tag, area and priority filters. Scope is the selected team's configured area paths within the selected iteration.
- **Inline edits:** owner, state, iteration, tags, estimate, priority and area. Changes appear immediately; rejected updates revert. A small saving indicator marks the row. Native selectors allow keyboard choice. Type-specific field support is checked before sending writes; process-specific restrictions remain enforced by Azure DevOps.
- **Selection:** individual, all visible and shift-range selection. Sorting and filtering preserve selection. The selected count includes hidden selected rows. Switching iterations clears selection.
- **Bulk editing:** assign, move, add/remove a tag, state, area and priority. Review current/proposed values before applying. Up to four work items update concurrently. Each item's success or failure is shown. Retry first refreshes failed items and requires another review.
- **Details:** click a title to open its side panel. View description, criteria, tags, parent, revision, heuristic quality checks and an Azure DevOps link. Parent links can open an item outside the loaded sprint.
- **Creation:** choose an actual project work-item type, apply an editable template, and set title, description, criteria, tags, area, sprint and parent. When a type lacks an acceptance-criteria field, criteria are included in the description and the dialog explains that behavior. Custom required fields outside this form may require the Azure DevOps editor.
- **Views:** save filters and columns locally. Saved views follow the currently selected sprint. ID and title remain visible.
- **Cleanup:** find unfinished, unassigned, unestimated, untagged, parentless and stale work, new work late in a sprint, completed parents with unfinished children, and previous-sprint unfinished items. The previous sprint is the previous chronologically sorted configured team iteration. Linked children needed for the completed-parent check are fetched explicitly.
- **Carry-over:** Prepare next sprint selects unfinished items in the current sprint. Adjust the checkboxes, then choose Sprint → and review the proposed move. Nothing moves automatically.
- **Demo mode:** explicit, isolated sample workspace. Mutations change only that circuit's in-memory demo data. Reopening/reloading starts a new demo. It never submits demo changes to Azure DevOps.

### AI copy/paste

There is no AI service call. SprintPilot puts the configured instructions and item data on the clipboard. Use an AI tool approved for the content you choose to copy.

1. Open a work item or select several, then choose **Copy for AI review**.
2. Paste into your chosen AI tool.
3. Copy its complete response, choose **Paste AI review** in SprintPilot, and preview.
4. Choose **Apply locally** to keep a temporary draft, or **Review Azure DevOps update** to proceed to the final current/proposed review.

Batch responses preserve `=== WORK ITEM 123 ===` and `=== END WORK ITEM 123 ===`. All seven headings are required exactly once in the prescribed order. Single-item responses can omit delimiters. Unknown, missing, duplicate or mismatched IDs reject the whole import. Extra commentary outside blocks is rejected. IDs refer to the items selected at paste time.

Technical notes and testing are appended to the proposed description. Questions stay in the review preview and are never converted into requirements. Local drafts are transient and disappear when the Blazor session ends; save or copy them before closing. Existing description formatting is intentionally converted to plain structured text in the AI workflow; the preview shows the resulting text before writes. For types without a dedicated criteria field, the proposed description includes those criteria.

### Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+K | Command palette |
| / | Focus search |
| Alt+Left / Alt+Right | Previous / next sprint |
| Ctrl+A | Select visible items when grid is focused |
| Esc | Close dialog or details |
| R | Refresh outside text-editing controls |

Text fields retain normal typing shortcuts. Ctrl+K intentionally overrides Edge's search shortcut inside the app. Alt+arrow navigation is limited to non-editing contexts.

## PWA and offline behavior

The web manifest includes 192px and 512px icons and standalone display. Install from Edge's app menu if your managed browser permits it. The local server must remain running. After a server restart, run the desktop shortcut to unlock the new session. The service worker does **not** cache work items, HTML pages or mutations. No offline Azure DevOps operation is promised.

## Build and tests

```powershell
dotnet build SprintPilot.sln -c Release
dotnet run --project tests/SprintPilot.UnitTests -c Release --no-build
dotnet run --project tests/SprintPilot.IntegrationTests -c Release --no-build
dotnet publish src/SprintPilot.Web -c Release --no-self-contained -o artifacts/publish
```

The two test projects are dependency-free executable test harnesses, **not** `dotnet test` discovery projects. They fail with a non-zero exit code. The integration harness uses a simulated HTTP handler and never contacts a live tenant. GitHub Actions runs the build, both harnesses, publishing and PowerShell syntax validation on Windows and Linux.

`global.json` selects stable .NET 10 SDKs using `latestFeature`. No extra third-party NuGet packages are explicitly referenced; the SDK can restore its own framework assets. Build outputs and local secrets are ignored by Git. The supplied archive contains source, not a platform-specific compiled executable.

## Architecture

| Project | Responsibility |
| --- | --- |
| SprintPilot.Domain | Work items, metadata, change models and local preference models |
| SprintPilot.Application | Provider ports, bulk coordination, AI parsing, quality rules and optimistic snapshots |
| SprintPilot.AzureDevOps | REST API 7.1, authentication, metadata cache, WIQL, relations, batching and revision-safe writes |
| SprintPilot.Infrastructure | Windows credentials, local JSON preferences and isolated demo provider |
| SprintPilot.Web | Blazor components, circuit session, local access gate, PWA and keyboard handling |

Read metadata is cached per circuit for 15 minutes; Refresh invalidates it. Sprint snapshots are held in memory for immediate cached switching while a fresh request runs. Cancellation prevents superseded sprint requests from replacing newer views. Each successful edit invalidates sprint snapshots. Credentials are snapshotted per circuit so changing organizations in one tab does not retarget another tab's edits.

Every existing-item write starts with a JSON Patch revision test. If a request times out after submission, the server may have applied it; refresh before retrying. Non-idempotent writes are never automatically replayed. Read operations have bounded retries for throttling/unavailability. Bulk changes are not a cross-item transaction.

## Limits and troubleshooting

To test Azure DevOps connectivity independently of the web application, run the parameterized diagnostic from the repository root:

```powershell
.\scripts\Test-AzureDevOpsConnection.ps1 -Organization "your-organization" -Project "your-project"
```

The script securely prompts for the PAT, performs read-only project and identity requests, and prints only sanitized status information. It does not contain organization, project, user, or token values and does not save the PAT.

- Azure DevOps **Services** (`dev.azure.com`) is supported; Azure DevOps Server/TFS URLs are not.
- Search is local to loaded sprint rows, not an organization-wide work-item search. Tags are read from work items and written through the work-item API; there is no standalone organization tag-administration screen.
- Estimate mapping tries Story Points, Effort, Size, then Remaining Work. For Tasks this can mean hours, not points; the UI labels the column Estimate.
- State names/categories are discovered. Category `Resolved` is not assumed finished; `Completed` and `Removed` are. Custom rules may still reject offered transitions.
- Quality is heuristic: title length at least 12, text presence, estimates greater than zero, testing keywords and 80% title-token overlap. Similarity checks cover loaded items, not the whole project. Weights are configurable; thresholds other than stale days are implementation defaults.
- Large sprints can incur substantial transfer/render costs. The grid is not virtualized. Launch and switching performance goals need measurement on your AVD; no 2–3 second or sub-500ms guarantee is made.
- A failed read does not silently discard inaccessible work items; it shows an error. Queries exceeding Azure DevOps WIQL service limits require narrowing the team area or sprint scope.
- If the port is occupied, Setup/Start refuse to control an unverified process. Choose another port. Stop checks application identity, published path, process start time and command line; it never kills all dotnet processes.
- If connection testing succeeds but workspace loading fails, check team metadata permissions, configured areas and iterations. Use Settings to correct the connection.
- If credentials expire, use Change connection and save a renewed PAT. To remove persisted credentials, use Windows Credential Manager; deleting the source checkout does not delete those entries.
- If a PWA window is locked after restarting the server, use the desktop shortcut once. If the server is stopped, start it first.
- Console diagnostics: from `artifacts\publish`, run `dotnet .\SprintPilot.Web.dll` temporarily. It still enforces local session access. Do not enable HTTP body or credential logging.

Microsoft references: [revision-checked work-item updates](https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/work-items/update?view=azure-devops-rest-7.1), [200-item batch reads](https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/work-items/get-work-items-batch?view=azure-devops-rest-7.1), [team iterations](https://learn.microsoft.com/en-us/rest/api/azure/devops/work/iterations/list?view=azure-devops-rest-7.1), [process-specific work-item definitions](https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/work-item-types/get?view=azure-devops-rest-7.1).
