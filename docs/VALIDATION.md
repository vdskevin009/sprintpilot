# Delivery validation

Validated with .NET SDK 10.0.401 on Linux.

| Check | Result |
| --- | --- |
| Seven-project Release build | Passed, zero warnings and errors |
| Unit executable | 43 checks passed |
| Simulated Azure DevOps HTTP integration executable | 19 checks passed |
| Framework-dependent local publish | Passed |
| Published local server startup | Passed |
| Original-release local HTTP access checks (not repeated for this planning-only update) | 10 passed: health identity, locked landing page, protected Blazor path, hostile Origin rejection, hostile Host rejection, invalid launch key rejection, valid session exchange, authorized app shell, standalone manifest, stylesheet delivery |
| Browser UI / visual verification | Not verified: managed browser rejected localhost with ERR_BLOCKED_BY_CLIENT |
| Windows Credential Manager and PowerShell lifecycle | Not executed in this Linux environment |
| Live Azure DevOps reads/writes | Not executed; no user credentials supplied |
| PWA installation in managed Edge | Not verified |
| Performance targets | Not benchmarked |

The two checked-in test executables require no Azure DevOps credentials. Their simulated requests validate data mapping, 200-item batching, relation expansion, team scope, metadata caching, authentication plumbing, safe error handling and revision-checked writes. They do not prove that a tenant's custom process rules or AVD policies permit every operation.

Before using live sprint data, complete `AVD-ACCEPTANCE.md` in an appropriate test project.

## Planning update

The planning update builds with zero warnings/errors. All 62 executable checks pass. Five sample planning horizons were rendered successfully from the actual Blazor component into the standalone preview. The preview script passed JavaScript syntax validation; real browser interactions were not verified. This session's earlier managed-browser localhost restriction remains a verification limitation.

Planning tests cover horizon boundaries, overlapping tags, deduplicated totals, unknown/invalid estimates, custom fields, per-person/per-sprint targets, zero-work members, 2,003-item pagination and incomplete-batch rejection. The planning dashboard makes no Azure DevOps mutations.
