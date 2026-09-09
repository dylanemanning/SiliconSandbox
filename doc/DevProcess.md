# Development Process

This document defines how Team 3 structures the Silicon Sandbox repository, how work moves
through branches, and how changes are reviewed and merged. It supersedes `devProcessWorkshop.md`,
which captured our initial planning from the Spring Dev Process Workshop.

---

## 1. Repository Architecture

Silicon Sandbox is a Unity 6 project (editor version `6000.3.8f1`, locked in `ProjectSettings`)
with a native C++ logic engine compiled as a plugin. The repository is organized so that Unity
manages everything it needs inside `Assets/`, while source that Unity should not import lives at
the repository root.

### Structure

```
SiliconSandbox/
├── Assets/                      Unity-managed project content
│   ├── Materials/               Shared materials and textures
│   ├── Prefabs/                 Reusable placeable objects (blocks, gates, wires)
│   ├── Plugins/
│   │   └── x86_64/              Compiled SiliconPlugin.dll consumed by Unity
│   ├── Scenes/                  MainMenu.unity, SampleScene.unity
│   ├── Scripts/                 Gameplay C# and the logic engine wrapper
│   ├── Settings/                Universal Render Pipeline assets and volume profiles
│   ├── TextMesh Pro/            Third-party package content, unmodified
│   └── InputSystem_Actions.inputactions
├── Packages/                    Unity Package Manager manifest and lockfile
├── ProjectSettings/             Editor, build, and version-lock settings
├── doc/                         Project documentation
│   ├── DevProcess.md            This document
│   ├── Final SDP.md             Software development plan
│   ├── Design Document.pdf
│   └── Verification & Validation.pdf
├── native/
│   └── SiliconPlugin/           C++ logic engine source and build configuration
├── .gitignore
└── README.md
```

### Directory descriptions

**`Assets/`** holds everything Unity imports and tracks with `.meta` files. Our own content is
kept separate from third-party content: `TextMesh Pro/` is package-managed and is never edited
by hand, so that package updates do not conflict with our work.

**`Assets/Scripts/`** is currently flat because the prototype is small. As the logic engine and
UI grow, it subdivides by system — `LogicEngine/`, `Player/`, `UI/` — rather than by file type,
so that related code stays together.

**`Assets/Plugins/x86_64/`** holds only the compiled `SiliconPlugin.dll`. Unity loads native
libraries from this path automatically based on the target architecture. The built binary is
committed so that team members without a C++ toolchain can open and run the project.

**`native/SiliconPlugin/`** holds the C++ source and build configuration for the logic engine.
This is the one place we deliberately depart from Unity's guidance to minimize root-level folders.
Unity generates `.meta` files for every file under `Assets/`, including source files it cannot
compile, so keeping C++ sources outside `Assets/` avoids meaningless metadata churn in version
control. Unity's own native plug-in documentation treats the compiled library, not its source,
as the project artifact.

**`doc/`** holds all project documentation in the repository rather than in external storage, so
that documentation is versioned alongside the code it describes.

**`Packages/` and `ProjectSettings/`** are Unity-generated but must be committed. `ProjectSettings`
carries our editor version lock, which keeps every developer on the same Unity version and prevents
the project-format upgrades that occur when a newer editor opens the project.

Build output, the `Library/` cache, logs, and generated IDE project files are excluded by
`.gitignore`, which is derived from GitHub's maintained Unity template.

### Standards and references

- [Best practices for organizing your Unity project](https://unity.com/how-to/organizing-your-project) — Unity's guidance on folder structure, naming, and separating third-party content.
- [Native plug-ins — Unity 6.3 Manual](https://docs.unity3d.com/6000.3/Documentation/Manual/plug-ins-native.html) — the supported layout for compiled native libraries and how Unity resolves them by architecture.
- [github/gitignore — Unity template](https://github.com/github/gitignore/blob/main/Unity.gitignore) — the basis for our exclusion rules.

---

## 2. Branching / Workflow Model

### Model

We use **GitHub Flow**: a single permanent branch, `main`, with short-lived feature branches
created from it and merged back through pull requests.

| Branch | Purpose |
|---|---|
| `main` | The single source of truth. Always in a state that opens in Unity and runs. Every feature branch starts here and returns here. Protected: changes arrive only by reviewed pull request. |
| `<initial>f_<featureName>` | A short-lived branch for one issue's work. Created from `main`, merged back to `main`, then deleted. |

Milestone and demo builds are marked with **git tags** on `main` rather than kept on a separate
branch, so that any past build remains reachable without maintaining a parallel history.

### Naming convention

Feature branches are named `<developer initial>f_<featureName>`, carried over from our initial
process:

- `gf_gateLogic`
- `jf_menus`
- `df_backups`

### Branch lifecycle

1. Create the branch from an up-to-date `main`.
2. Commit work in logical units with descriptive messages.
3. Open a pull request when the issue's completion condition is met.
4. Merge after approval.
5. **Delete the branch immediately after merge**, both locally and on the remote, so that stale
   branches do not accumulate and no one branches from an abandoned line of work.

Feature branches should not live longer than roughly one week. Unity scene and prefab files are
serialized YAML that merges poorly, so the practical cost of a long-lived branch is far higher
in this project than in a typical codebase.

### Handling Unity merge conflicts

Unity ships **UnityYAMLMerge**, which resolves `.unity` and `.prefab` conflicts semantically
rather than line-by-line. Each developer configures it as the merge tool for this repository.
Where two people must edit the same scene, the work is split across separate prefabs and
composed in the scene afterward, which keeps conflicts out of the scene file entirely.

Reference: [Smart merge — Unity 6.3 Manual](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html)

### Change from our previous model

Our workshop plan specified a simplified gitflow with a permanent `integration` branch between
feature branches and `main`. We are retiring `integration`, along with the stale `Prototype`
branch, for three reasons.

First, GitHub's issue-closing keywords only take effect when a pull request targets the
repository's default branch. Under the previous model, `Closes #7` on a feature branch merging
into `integration` would link the issue but never close it, leaving our issue tracker permanently
out of date with the actual state of the work.

Second, gitflow is designed for software that ships explicitly versioned releases and supports
multiple versions simultaneously. Its author, Vincent Driessen, now recommends that teams
practicing continuous delivery adopt a simpler workflow such as GitHub Flow rather than force
gitflow to fit. We ship one continuously developed artifact toward a demo, not a versioned
product line.

Third, the intermediate branch adds merge surface without adding safety. In a Unity project,
every additional day of divergence increases the chance of a scene or prefab conflict. The
protection `integration` was meant to provide — keeping `main` clean — is delivered more
directly by requiring review before any merge to `main`.

References:
- [GitHub flow — GitHub Docs](https://docs.github.com/en/get-started/using-github/github-flow)
- [A successful Git branching model](https://nvie.com/posts/a-successful-git-branching-model/) — see the author's 2020 reflection note.

---

## 3. Pull Request Process

### Naming convention

Pull request titles follow:

```
[#<issue number>] <Imperative summary of the change>
```

For example: `[#7] Implement AND, OR, and NOT gate logic`

The leading issue number makes the pull request list readable against the project board, and the
imperative summary matches the convention used for commit messages.

### Linking issues to pull requests

Every pull request resolves at least one issue, and the link is made with a GitHub closing
keyword in the pull request description. Because our feature branches target `main`, which is the
default branch, the keyword closes the issue automatically on merge.

Supported keywords are `close`/`closes`/`closed`, `fix`/`fixes`/`fixed`, and
`resolve`/`resolves`/`resolved`.

**Example.** Issue #7 is *Implement AND, OR, and NOT Gate Logic*. The work is done on branch
`gf_gateLogic`, and the pull request is opened against `main`:

> **Title:** `[#7] Implement AND, OR, and NOT gate logic`
>
> **Description:**
>
> Closes #7
>
> Adds `EvaluateGate(gateType, inputs)` to the logic engine, covering AND, OR, and NOT.
> Each gate is verified against its full truth table.
>
> **Verification**
> - Project opens in Unity 6000.3.8f1 with no import errors
> - Truth-table tests pass for all three gates across every input combination
> - No console errors on entering Play mode in SampleScene

When a pull request contributes to a parent issue without completing it, it references the parent
without a keyword (`Refs #35`) and closes only the sub-issue it finishes.

Reference: [Linking a pull request to an issue — GitHub Docs](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue)

### Code review policy

Every change reaches `main` through a pull request. Direct commits to `main` are not permitted.

**Approval requirement.** Each pull request requires **one approving review from another
developer** before it may be merged. Authors do not approve their own work. With a three-person
team, one reviewer keeps changes moving while still guaranteeing that no code enters `main`
unseen by a second person.

**Review turnaround.** Reviewers respond within 24 hours. Because our issues are scoped to
individual, independently completable units of work, pull requests are expected to be small
enough to review in a single sitting. A pull request that cannot be reviewed in one sitting is a
signal that the underlying issue should have been split.

**Merge responsibility.** The author merges after receiving approval, and is responsible for
deleting the branch afterward. Feature branches are squash-merged so that `main` carries one
commit per completed issue and its history reads as a list of delivered work.

**Reviewer responsibilities.** A reviewer confirms that the change does what the linked issue
describes, that the issue's stated completion condition is actually met, that no ignored files
have been committed, and that `.meta` files accompany any new asset. A missing `.meta` file
breaks the project for every other developer, so this is checked explicitly.

### Pre-review verification

We do not currently run automated continuous integration. Unity builds on GitHub Actions require
license activation through repository secrets, which we have scoped as future work rather than a
prerequisite for development. In its place, the **author** completes the following checklist
before requesting review, and records the result in the pull request description:

- [ ] Project opens in Unity `6000.3.8f1` with no import errors
- [ ] Affected scenes enter Play mode with no console errors
- [ ] Logic engine unit tests pass locally
- [ ] No ignored artifacts staged (`Library/`, `Builds/`, `Logs/`, generated `.csproj`/`.slnx`)
- [ ] `.meta` files committed for all newly added assets
- [ ] The linked issue's completion condition is satisfied

The reviewer treats an incomplete checklist as grounds to return the pull request without review.
Automating this checklist as a GitHub Actions workflow is tracked as a future improvement.

---

## 4. Project Management

All development work is tracked through a single GitHub Project attached to the repository, which
presents the same set of issues in three views:

| View | Purpose |
|---|---|
| [Next 2 Weeks](https://github.com/users/dylanemanning/projects/1/views/5) | Default view. Filtered to the work committed for the current two-week cycle. |
| [Roadmap](https://github.com/users/dylanemanning/projects/1/views/3) | Timeline of planned work across the remainder of the semester. |
| [Backlog](https://github.com/users/dylanemanning/projects/1/views/1) | Every issue in the system, unfiltered. |

### Issue ownership

Individual developers are responsible for tracking the issues assigned to them. The assignee has
sole control over moving an item through its states and over setting its due date, priority, and
other metadata. This keeps ownership unambiguous: at any point, exactly one person is accountable
for the state of a given issue being accurate.

### Issue structure

Larger bodies of work are recorded as parent issues with sub-issues beneath them, using GitHub's
parent/sub-issue feature, so that a broad deliverable is tracked as a set of independently
completable units rather than one long-running item. Our current parent issues are #33 Create all Blender Models, 
#32 Gameplay, #34 UI, #35 Implement Circuit Logic, #37 Save and Load Components, and #38 Project Management.

### Issue creation 

Any individual developer may create a new issue and assign it to the relevant developer. Each issue must only 
address one problem, bug fix, or feature. If it pertains to a larger sub-group of issues, assign it to that parent issue. 
The description must also have a clear completion condition. Since each assignee has direct ownership over their 
issues and when they are completed, they are responsible for setting a timeline for completion. The issue will then
be automatically added to a two-week cycle when it comes within two weeks of its due date.

---

## 5. Closing Out a Two-Week Cycle

### Weekly review

The development team meets every week to address which issues have been closed, which are due in
the coming week, and any blockers a developer needs assistance on.

### Cycle close

Every other meeting closes out a two-week cycle and includes an extended section dedicated to
issues that have fallen behind schedule, adjustments to priority, changes to due dates, and any
other major shifts to the workflow. This is where the two-week commitment is reconciled against
what was actually delivered.

### Closing an issue

An issue is closed by its assignee rather than by team vote. This is not unilateral in practice:
work reaches `main` only through a reviewed pull request, and a pull request carrying a closing
keyword closes its linked issue on merge. The assignee's judgment therefore operates inside the
review gate described in section 3, not around it. An issue is eligible to close only once the
completion condition stated in its description is satisfied.

### Unfinished issues

If an issue is still open at the close of a cycle, its due date will be extended by two weeks 
so that it is automatically included in the next two-week cycle. If the assignee determines that the
task is too difficult or long, they can split the issue into sub-issues and assign reasonable
due dates according to their own judgement. If the completion of that issue is a prerequisite for 
the completion of another individual's issue, they can work with the assignee to adjust priorities
or complete the work.

---

## 6. Refreshing Project Views

The Next 2 Weeks and Roadmap views are both derived from issue fields and update automatically as
issues are edited. Neither view is maintained by hand.

Because they are generated rather than authored, keeping them accurate is a matter of verification
rather than upkeep. The team reviews both views against actual progress at the weekly meeting, and
the biweekly cycle-close meeting is where discrepancies are corrected — dates adjusted, priorities
changed, and items moved between cycles — so that neither view drifts from the real state of the
project. The team may discuss these changes as needed so that everyone is on the same page, but each 
individual developer is responsible for making corrections on their own assigned issues. A slipped roadmap
date will typically be handled by adding those open issues to the next sprint, in addition to the 
already scheduled issues. However, if slippage continues and the work or scope becomes unmanageable,
the team can discuss how to reduce the scope of the project and adjust the issue backlog accordingly.
