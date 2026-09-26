# SprintPilot

## Requirements source of truth

Read `REQUIREMENTS.md` before planning or changing SprintPilot. Treat it as the canonical source for product intent, scope and requirement status.

For every user-requested change:
- identify affected requirement IDs, or add a new stable ID before/with implementation;
- do not mark a request implemented merely because it was discussed;
- update requirement status and implementation notes together with the code change;
- if a newer explicit decision conflicts with an older requirement, preserve the older item and mark it `Superseded`;
- record ambiguity under **Open questions / Needs confirmation** instead of inventing behavior;
- after merge/verification, update the requirement code reference so it stays aligned with the repository.

Read `README.md` and relevant files under `docs/` before changing behavior.

Azure DevOps is the source of truth. SprintPilot must remain a companion that makes work safer and faster, not a second authoritative work-item store.

Preserve explicit review before Azure DevOps writes. Keep revision-safe writes, truthful success/failure reporting, visible-item scoping for bulk/AI actions, and conservative cleanup rules.

Do not expose PATs to the browser or logs. Preserve loopback-only local hosting and the existing Windows credential/session protections.

Run the relevant release checks before claiming completion:
- `dotnet build SprintPilot.sln -c Release`
- `dotnet run --project tests/SprintPilot.UnitTests -c Release --no-build`
- `dotnet run --project tests/SprintPilot.IntegrationTests -c Release --no-build`
- publish `src/SprintPilot.Web` when delivery behavior changes.

Do not claim AVD/browser/real-tenant behavior was verified unless the corresponding acceptance check actually ran.
