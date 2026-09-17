# AVD acceptance checks

These checks require Windows/Edge and, where stated, a non-production Azure DevOps project. They were not executed against the user's AVD or tenant during delivery.

## Local lifecycle

- [ ] Setup succeeds with a stable .NET 10 SDK without elevation.
- [ ] Desktop shortcut opens one process; repeated launch opens no duplicate server.
- [ ] Another application on the port is not stopped or replaced.
- [ ] Stop terminates only the verified SprintPilot process.
- [ ] Paths containing spaces work for Setup, Start, Stop and the shortcut.
- [ ] Two Windows users using different ports cannot read each other's session files.
- [ ] Protected pages cannot be used from a fresh browser without launch authorization.
- [ ] Missing configuration opens the welcome screen and demo mode works.
- [ ] Credential Manager persists the token across restarts; no PAT appears in source, local JSON, browser responses or diagnostics.

## Workspace

- [ ] Correct team areas and configured iterations load; another team's work stays out.
- [ ] Owner, state, iteration, tags, estimate, priority and area edits succeed on supported fields.
- [ ] A rejected edit rolls back and shows an actionable safe error.
- [ ] Concurrent change in Azure DevOps causes a revision rejection, not lost updates.
- [ ] Individual, all-visible and shift-range selection work; sort/filter retain selected IDs.
- [ ] Hidden selected rows are included in the preview and selected count.
- [ ] Bulk partial failure shows both successes and failures; retry fetches new values before review.
- [ ] Sprint switching cancels outdated reads; cached results refresh without a page reload.
- [ ] Side panel keeps the grid visible; parent and Azure DevOps links work.
- [ ] Creation works for PBI/User Story, Bug and Task; process-required fields reject safely.
- [ ] Saved views, columns, theme, templates and quality weights survive restart.
- [ ] Cleanup and carry-over find the expected work; nothing moves before Apply.

## AI review

- [ ] Single and batch exports contain the correct IDs and all useful current content.
- [ ] Unknown, missing, duplicate and malformed IDs are rejected.
- [ ] Proposed text is rendered safely, not as executable HTML.
- [ ] Technical notes/testing persist in the description; questions remain review-only.
- [ ] Local drafts do not change Azure DevOps.
- [ ] Final review shows current/proposed values and preserves revision conflict protection.
- [ ] Unsupported dedicated criteria fields are folded into the proposed description visibly.

## Browser and performance

- [ ] Verify layouts at 1280px, 1440px and a narrower AVD window, in dark/light/system themes.
- [ ] Verify keyboard focus, modal tab trapping, Escape and all documented shortcuts.
- [ ] Verify clipboard permission behavior in managed Edge.
- [ ] Verify PWA installation if policy permits; stopped server clearly prevents online work.
- [ ] Measure time to usable workspace on cold/warm launch and typical sprint switches.
- [ ] Test a realistically sized sprint; record latency and UI responsiveness, not just sample-data behavior.

## Planning dashboard

- [ ] Verify full team backlog, not only selected sprint rows, is loaded.
- [ ] Confirm selected fields use compatible units; hour comparisons stay gated until confirmed.
- [ ] Classify multi-tag items and reconcile each split chart with unique total, including Unclassified.
- [ ] Compare full membership and split modes; verify filters do not reallocate hidden shares.
- [ ] Verify current/next/next-week boundary dates and root/overdue backlog behavior.
- [ ] Verify zero-work members, unknown estimates, unassigned work and per-sprint leave overrides.
- [ ] Click bars and capacity rows; inspect full item estimates and open details.
- [ ] Change team and restart; confirm planning profiles remain isolated and persist.
- [ ] Verify the page and preview layouts at narrow and desktop sizes in light/dark themes.
