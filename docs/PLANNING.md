# Planning overview

Open **Planning overview** from the sidebar after connecting or entering demo mode. This page uses backlog items and bugs directly; it does not create or require tasks. Charts are read-only. Click a bar or a person's current/next workload to inspect its work items, then click a title for the existing details panel.

## Install this update

Stop the currently running instance with `scripts/Stop-SprintPilot.ps1`, replace the source files in that checkout with the updated archive contents, and run `scripts/Setup-SprintPilot.ps1`. Existing local settings and credentials are outside the checkout and remain available. Planning is a new sidebar page; no hosted deployment is required.

## First setup

1. Open Planning settings. Pick the project backlog/bug types to include. Tasks are always excluded; select leaf work-item types so parent rollups are not added to their children.
2. Check the estimate field for each type. The default is the same discovered estimate field used in the grid. A different numeric field observed on the loaded items can be selected for planning. This does not change the grid field or Azure DevOps data.
3. Confirm **the estimate values are hours** only if that is how your team actually records them. SprintPilot does not convert story points to hours. Changing a field clears the confirmation. Without confirmation, values are labeled units and person/hour comparisons are suppressed; do not compare mixed estimate scales.
4. Select or enter ordinary Azure DevOps tag names and mark them as applications and/or initiatives. Tag names are case-insensitive. These classifications exist only in SprintPilot; Azure DevOps tags are not renamed or rewritten.
5. Set default minimum/maximum hours per person per sprint, initially 50–60. Override a named person's current or next sprint for leave, support, availability, or other commitments. Overrides are keyed by identity ID and iteration ID, so they do not accidentally carry forward to every new sprint.
6. Save. Configuration is isolated by organization, project and team in the existing preferences file.

## Period definitions

| Horizon | Included work |
| --- | --- |
| Current sprint | Open selected-type items in the team's dated iteration active today |
| Next sprint | Open items in the earliest dated team iteration starting after the current sprint ends; if no current sprint exists, the first future iteration |
| Next week | Open items in all dated team sprints overlapping next Monday through Sunday; full item estimates, no daily allocation or prorating |
| Backlog | Open work outside a current/future dated team sprint: root backlog, unknown or undated iterations, and overdue unfinished sprint work |
| Total | All open selected-type items in the selected team's configured area scope, counted once by ID |

The periods overlap. In a two-week sprint, Next week can have exactly the same work as Current sprint. Do not add the cards together. Today comes from the local machine calendar; Azure iteration start/finish values are treated as inclusive calendar dates. A missing current/next dated iteration is explicitly labeled, not silently replaced with an arbitrary sprint. If active team iterations overlap, the latest-starting active iteration is chosen for Current; Next week includes every overlapping iteration.

Completed and Removed categories are excluded. Resolved is still open unless the process puts it in a completed category. Unknown states are included conservatively and flagged. The numbers represent whole-item estimates for open items, **not remaining hours**, logged time, measured utilization, or promises of when someone will finish.

## Multi-tag arithmetic

The default **Split equally** mode divides each item's estimate by its number of matching tags within that dimension. An item of 12 hours tagged to two applications contributes 6 hours to each. If it also has one initiative tag, it contributes 12 hours to that initiative. Applications and initiatives are separate views of the same 12 hours, not 24 hours of work.

Each complete split chart, including Unclassified, reconciles to the selected horizon's unique estimate total. The displayed linked-item counts can overlap and must not be added. Selecting chart tags only hides other rows; it does not redistribute the hidden share or change the headline totals and people table.

Optional **Full membership** mode puts the full estimate under every matching tag. Rows overlap and are explicitly labeled non-additive. The unique headline total never changes merely because the allocation mode changes.

Unclassified retains items without a configured tag for that dimension. Null, invalid, negative and non-finite estimates are counted as unestimated and contribute no known hours. They are never presented as evidence of available capacity. A zero estimate is an explicit zero, not a missing estimate.

## People

The table covers current and next sprint independently of the selected chart horizon and filters. It shows unique item count, known estimate, unestimated count, target band and status. Every team member appears, including people with zero items. Unassigned work and assignees outside the selected team are also visible.

Known hours above the maximum show Above target. Otherwise, any missing estimates show Estimates missing rather than Room for work. Fully estimated work below the minimum shows Room for work; within the inclusive band shows Within target. These are planning hints, not productivity/performance judgments. A zero target can represent someone unavailable.

## Retrieval and freshness

The dashboard queries all selected-type open work in the team's configured area paths across all iterations, not just already-open grid rows. ID-based query pagination uses up to 2,000 IDs per query, followed by 200-item read batches. Reads fail visibly if a page does not advance, a read omits an item, or the configured 50,000-item safety limit is exceeded. No partial dashboard is substituted. Existing snapshots are labeled as previous data after a refresh failure.

A scan is not a transaction: Azure DevOps can change while pages are read. Refresh before acting on the plan. The page shows its refresh time. Returning to the page reloads data. Changes made through the work-item panel or another client require Refresh planning to update the charts. Planning settings apply only after the requested data has been loaded successfully and the preferences save succeeds.

## Example preview and tests

The supplied standalone `SprintPilot-planning-preview.html` contains synthetic data only. It is generated by server-rendering the actual Blazor PlanningDashboard component for all five horizons. Period, tag filter and drilldown interactions are added locally; it makes no API requests and cannot change Azure DevOps.

Regenerate from the repository root:

```powershell
dotnet run --project tools/SprintPilot.Preview -c Release -- SprintPilot-planning-preview.html
```

The unit and HTTP-adapter test executables cover unique totals, overlapping tags, missing hours, custom fields, zero-work members, capacity overrides, calendar boundaries, multi-page retrieval, and incomplete-read failures. Visual browser behavior and a live Azure DevOps connection still need AVD validation.
