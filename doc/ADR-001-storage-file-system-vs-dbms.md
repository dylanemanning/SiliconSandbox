# ADR-001: Local File System vs. DBMS for World/Project Storage

**Status:** Accepted
**Date:** 2026-08-30
**Owner:** Gray Dufilho

## Context

CDR feedback asked whether world/project data (referenced in FR-1: Project Selection and NFR-7: Autosave) should be stored via a database management system instead of flat files. This decision was previously only tracked informally in the team's feedback-disposition spreadsheet and was not reflected anywhere in the project's written documentation.

## Decision

Store world/project files on the local file system rather than using a DBMS.

## Rationale

- Per assumption A4 (Final SDP), users run the game on a personal computer via Steam. There is no server or multi-user access pattern that a DBMS would be solving for.
- The team operates under constraints C1 (limited development time) and C2 (limited initial game-dev experience). Standing up and maintaining a DBMS (schema, migrations, query layer) spends time better used on core simulation and gameplay features.
- A flat file format is simpler to implement and debug, and maps directly onto FR-1 (open a saved project) and NFR-7 (periodic autosave to a local backup folder) as already written.

## Consequences

- World/project files are structured files (e.g. JSON) on local disk under a per-user save directory; the autosave/backup behavior in NFR-7 operates on these files directly.
- Multi-machine sync and cross-user sharing are explicitly out of scope for this decision. If cloud storage or build-sharing (E1 in the SDP's "Under Evaluation" scope) becomes a priority later in the semester, file I/O should be revisited behind an abstraction so it can be swapped without a full rewrite.
