# Test Documentation

This directory holds all verification and validation documentation for SiliconSandbox. It sits
alongside `doc/DesignDocs`, which holds design-phase documentation; `doc/` is organised into child
directories by category so files are easy to locate rather than unsorted at the top level.

## Contents

| File | Purpose |
| --- | --- |
| `Verification Test Inventory.md` | The full inventory of verification tests (TC-1.x – TC-10.x), their owners, tooling, automation status and evidence. Maintained throughout the semester. |
| `ManualTestCaseTemplate.md` | Template for documenting a manually executed test case. |
| `RCATemplate.md` | Template for a root cause analysis and postmortem. |
| `TC-*.md` | Completed manual test reports, named by the test case they execute. |
| `Verification & Validation.pdf` | The Part 1 verification and validation plan. |

Root cause analyses are **not** stored as files in this directory. Each RCA is written into the
GitHub issue for the defect it concerns, so that the analysis lives with the defect, its discussion
and its fix. The table below is the index of those analyses.

## Root Cause Analysis Log

Every RCA completed during the semester is recorded here. Severity follows the rubric below.

| Date | Issue | Severity | Defect summary | Root cause | RCA | Author |
| --- | --- | --- | --- | --- | --- | --- |
| _pending_ | — | — | — | — | — | Gray |

Column notes:

- **Issue** — link to the GitHub issue, e.g. `[#57](https://github.com/dylanemanning/SiliconSandbox/issues/57)`.
- **Root cause** — the underlying cause in a short phrase, not the symptom.
- **RCA** — permalink to the specific issue comment containing the analysis.

## Severity Rubric

Severity determines how much documentation a defect requires. This is our project-specific reading
of the course's documentation guide.

| Severity | Documentation required | What counts, for this project |
| --- | --- | --- |
| **High** | Bug report **+ RCA** | Incorrect logic simulation output (a gate, wire or netlist producing a wrong result); loss or corruption of a saved project or backup; a crash or hang during normal play. |
| **Medium** | Bug report | A feature that fails but has a workaround — a menu path that errors while another route works, a component that must be replaced to behave correctly. |
| **Low** | Optional or lightweight notes | Visual and UI issues, typos, cosmetic misalignment, narrow edge cases with no functional impact. |

We do not expect more than 2–3 postmortem entries per build checkpoint.
