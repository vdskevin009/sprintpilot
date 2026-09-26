# SprintPilot

## Requirements source of truth

Read `REQUIREMENTS.md` before planning or changing the product. Treat it as the canonical source for product intent and requirement status.

For every user-requested product change:
- identify the affected requirement IDs, or add a new stable ID before/with implementation;
- do not mark a request implemented merely because it was discussed;
- update the requirement status and implementation notes together with the code change;
- if a newer explicit decision conflicts with an older requirement, keep the old item and mark it `Superseded`;
- record unresolved ambiguity instead of inventing behavior;
- after merge/verification, update the requirement code reference so the document stays aligned with the repository.

## Repository-specific rules

Read `README.md` and the relevant files under `docs/` before changing behavior. Azure DevOps is the source of truth; SprintPilot is a local companion. Preserve explicit review/revision safety for writes, AVD/local-session security, and existing no-paid-service assumptions. Do not weaken credential handling or logging protections. Run the relevant build, application/integration harnesses and publish checks for source changes, and keep live Azure DevOps/AVD acceptance clearly separate from simulated automated tests.
