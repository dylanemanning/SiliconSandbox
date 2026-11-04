# RCA: (Defect title)

> Paste this into the GitHub issue for the defect as a comment, filled in. Keep the headings —
> the five questions are required by the assignment and graded against.

**Issue:** #NN
**Severity:** High / Medium / Low (see the rubric in `doc/TestDocs/README.md`)
**Author:** <name>
**Date:** <YYYY-MM-DD>
**Fix:** PR #NN, merged <date>

## Defect Summary

One or two sentences: what the software did, what it should have done, and what the user-visible
impact was.

## Root Cause

The underlying cause, not the symptom. State the specific code, assumption or gap in process that
allowed the wrong behaviour to exist.

## 1. How was the defect discovered?

Who found it, under what circumstances, and at what point in the pipeline — a CI run, local
development, a manual test pass, someone playing the build.

## 2. Which test exposed the issue?

Name the test case ID from the Verification Test Inventory (e.g. TC-3.2.1). If no existing test
caught it, say so explicitly and state which test *should* have — that gap is itself a finding, and
it becomes the regression test below.

## 3. How was the fix verified?

What was changed, and what evidence shows it worked. Link the CI run, the passing test output, or
the manual test report. "It seemed to work" is not verification.

## 4. What regression test prevents recurrence of this issue?

The test that fails on the old code and passes on the new.

- **Test ID:**
- **Level:** Unit / Integration / System
- **Location:** file path in the repo
- **Automated:** Yes / Partial / No
- **In CI:** Yes / No — if no, the date it will be
- **Added to the Verification Test Inventory:** Yes / No

## 5. Are there any other portions of the codebase where this issue may still occur?

List the places the same *class* of mistake could live, not just the one line that was wrong. Say
what was checked and what was found for each, and open follow-up issues for anything unresolved.

| Location | Same risk? | Checked | Action |
| --- | --- | --- | --- |
|  |  |  |  |

## Postmortem

- **What went well** — what limited the damage or caught it before release.
- **What went badly** — what let it reach the branch it reached.
- **Process changes** — concrete, assigned changes. A test added to CI, a review step, a template
  field. Not "be more careful."
