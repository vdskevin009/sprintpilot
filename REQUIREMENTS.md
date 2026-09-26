# SprintPilot — Canonical Requirements

> **Source of truth for product requirements.** Read this file before changing SprintPilot. Update it whenever product intent, scope, implementation status, or a decision changes.

## Baseline

- **Repository:** `vdskevin009/sprintpilot`
- **Default branch:** `main`
- **Baseline verified:** 2026-09-26
- **Code reference:** `583e9b0279b398e8af6ba8e27010dbfa7a337753`
- **Baseline evidence:** current code/README at the reference above; latest merged change covers people-first time off, capacity impact and sprint refresh stability.

## Requirement lifecycle

Statuses: `Proposed`, `Accepted`, `In progress`, `Implemented`, `Verified`, `Deferred`, `Superseded`, `Rejected`.

Rules:
1. Add every new user requirement here before or with implementation.
2. Never infer missing business requirements; use **Open questions / Needs confirmation**.
3. Mark `Implemented` only when the code path exists; mark `Verified` only after relevant checks or acceptance testing.
4. New explicit decisions supersede older conflicting requirements; preserve the old requirement and mark it `Superseded`.
5. After implementation, update the requirement status, implementation notes and code reference.
6. Azure DevOps remains the system of record for work items; SprintPilot is a companion, not a replacement.

## Core product

| ID | Requirement | Status | Implementation notes |
|---|---|---|---|
| SP-001 | SprintPilot must be a local Azure DevOps sprint-management companion focused on reducing repetitive clicks and improving sprint visibility. | Verified | .NET 10 / Blazor Interactive Server local app. |
| SP-002 | SprintPilot must not require Azure hosting, a database, an AI subscription or an AI API key for its core workflow. | Verified | Current architecture. |
| SP-003 | Azure DevOps remains the source of truth; writes must be explicit, reviewable and revision-safe. | Verified | Existing-item writes use revision tests and review flows. |
| SP-004 | The app must be usable on Windows AVD and launch locally with a safe per-user session model. | Implemented | Setup/start/stop/update scripts and local access gate exist; real AVD acceptance remains required. |
| SP-005 | Performance claims must not be presented as measured guarantees until tested on the target AVD. | Accepted | Explicit delivery constraint. |

## Planning and capacity

| ID | Requirement | Status | Implementation notes |
|---|---|---|---|
| SP-PLAN-001 | Planning overview must compare application/initiative tags across current sprint, next sprint, next week, backlog and total. | Implemented | Planning overview exists. |
| SP-PLAN-002 | Show each person's current/next workload against configurable per-sprint targets. | Implemented | Initial targets documented as 50–60h. |
| SP-PLAN-003 | Time off and capacity impact must be represented consistently in people-first planning views. | Implemented | Included in baseline merge. |
| SP-PLAN-004 | Refresh/sprint switching must not let stale requests replace a newer selected sprint. | Verified | Cancellation/snapshot logic documented. |

## Sprint workspace

| ID | Requirement | Status | Implementation notes |
|---|---|---|---|
| SP-WS-001 | Support previous/current/next sprint navigation plus searchable iteration selection. | Implemented | Current workspace behavior. |
| SP-WS-002 | Support search and filters for owner, active-only, blocked, application, type, tag, area and priority. | Implemented | Current workspace behavior. |
| SP-WS-003 | Application tags must be visually/semantically separated from ordinary tags; configured blocked tags should map to the Blocked concept rather than duplicate filter noise. | Implemented | Current filter model. |
| SP-WS-004 | Allow inline editing of owner, state, iteration, tags, estimate, priority and area with safe rollback on rejected updates. | Implemented | Current inline edit contract. |
| SP-WS-005 | Selection-based bulk, Smart Fix and AI actions must apply only to currently visible checked rows. | Verified | Current selection contract. |
| SP-WS-006 | Work-item details must expose description, criteria, tags, parent, revision, quality checks and Azure DevOps link without forcing navigation away. | Implemented | Side panel exists. |
| SP-WS-007 | Carry-over to next sprint must always be explicitly reviewed; unfinished items must never move automatically. | Verified | Prepare-next-sprint flow. |

## Bulk editing and safety

| ID | Requirement | Status | Implementation notes |
|---|---|---|---|
| SP-BULK-001 | Bulk changes must preview current/proposed values before writing. | Verified | Existing review flow. |
| SP-BULK-002 | Each item must report success/failure individually; retries must refresh failed items and require another review. | Implemented | Current coordinator behavior. |
| SP-BULK-003 | Non-idempotent writes must not be automatically replayed after ambiguous timeout outcomes. | Verified | Current Azure DevOps adapter contract. |

## Cleanup workflows

| ID | Requirement | Status | Implementation notes |
|---|---|---|---|
| SP-CLEAN-001 | Sprint cleanup must identify unfinished, unassigned, unestimated, untagged, parentless/stale and other actionable work without silently modifying it. | Implemented | Current cleanup workspace. |
| SP-CLEAN-002 | Branch cleanup must be conservative and exclude default, locked, active-PR and long-lived branch patterns from automatic recommendation. | Verified | Current recommendation rules. |
| SP-CLEAN-003 | Branch deletion must require confirmation and use the reviewed object ID so changed branches are rejected safely. | Verified | Current deletion contract. |
| SP-CLEAN-004 | PR cleanup must use conservative recommendation rules and re-check active state/source commit before abandoning a PR. | Verified | Current PR cleanup contract. |

## AI-assisted workflows

| ID | Requirement | Status | Implementation notes |
|---|---|---|---|
| SP-AI-001 | SprintPilot must not make a paid/embedded AI service call for AI review; it may prepare/copy structured prompts and import validated responses. | Verified | Copy/paste workflow. |
| SP-AI-002 | AI imports must reject malformed, duplicate, mismatched or unknown work-item blocks rather than partially guessing. | Verified | Parser contract. |
| SP-AI-003 | Questions/missing information from AI review must never be silently converted into requirements. | Verified | Explicit current behavior. |
| SP-AI-004 | Magic Orchestration may suggest reassignment only among deterministic plausible candidates and must require individual Accept/Ignore plus the normal final Azure DevOps review. | Implemented | Current orchestration workflow. |

## Credentials and security

| ID | Requirement | Status | Implementation notes |
|---|---|---|---|
| SP-SEC-001 | Prefer Windows Credential Manager for PAT storage; never expose stored PAT values to the browser. | Verified | Current credential design. |
| SP-SEC-002 | Bind local server only to loopback and protect sessions against unrelated local web origins. | Verified | Host/origin restrictions + private launch capability. |
| SP-SEC-003 | Do not log request bodies, credentials, raw Azure error bodies or work-item content. | Verified | Current logging contract. |

## Delivery / testing

| ID | Requirement | Status | Implementation notes |
|---|---|---|---|
| SP-TECH-001 | Build, unit harness, integration harness and publish must pass for release checks. | Verified | CI currently runs these checks. |
| SP-TECH-002 | Integration tests must remain simulated and must not contact a live Azure DevOps tenant. | Verified | Current harness behavior. |
| SP-TECH-003 | AVD-specific browser, credential-manager and tenant interactions still require explicit acceptance testing. | Accepted | Current delivery limitation. |

## Open questions / Needs confirmation

- None recorded at baseline. Add unresolved requests here rather than guessing.

## Decision log

| Date | Decision | Result |
|---|---|---|
| 2026-09-26 | Establish `REQUIREMENTS.md` as SprintPilot's canonical requirement source. | Accepted |
| 2026-09-26 | Baseline requirements against `583e9b0279b398e8af6ba8e27010dbfa7a337753`. | Accepted |

## Maintenance checklist

Before implementation: read this file, identify affected IDs, add new IDs, and record conflicts.

After implementation: update statuses/notes, run relevant checks, update the code reference after merge, and keep README/docs aligned without treating them as the requirement source of truth.
