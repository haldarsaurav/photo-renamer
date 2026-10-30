# Photo Renamer audit — 2026-10-30

Scope: local review of the photo and folder rename engines, history validation, rollback and recovery, UI state management, and the new dark interface. This is a focused code and regression review, not a third-party security certification.

## v1.1 redesign verification

The [Unslop design report](docs/REDESIGN.md) records the audit, direction and
nine visual passes. The photo and folder transaction engines were unchanged.
The 110-assertion regression suite passed, the native app was rebuilt, and
eight generated photos were renamed across two folders and restored with
Undo through the actual UI. Both numbering positions, the shared-folder-name
preview and F1 Help were inspected with isolated sample files and history.

Bundled IBM Plex fonts are loaded privately into the process without system
installation or runtime downloads. Releases include their OFL license. Key
text and action contrast pairs were measured above 4.5:1. The public-source
scan was rerun, and new screenshots and video were checked for personal data.
The real-app walkthrough and HTML reconstruction used for the Unslop story
are explicitly distinguished in the README and media documentation.

## Fixed or strengthened

1. **Directory replacement detection.** The earlier engine used canonical paths as directory identity. Replacing a folder at the same path could evade that comparison if photo size and timestamps also matched. New plans and histories additionally record Windows volume and directory IDs, and validate them before mutation and during recovery. A regression recreates this exact case.
2. **Folder operations use a journal and a shared lock.** Folder renaming now stages directories under unique temporary sibling names, then applies the final names. The app retains a durable journal before moving anything. Undo, cancellation, failures, and interrupted recovery use the same name restoration flow. Photo and folder operations share one operation lock and one latest successful Undo.
3. **Unsafe folder batches are rejected before mutation.** Checks cover duplicate physical folders, parent-plus-child selections, drive roots, selected folder links, invalid or overlong names, reserved device leaves, target files/directories, and unexpected directory replacement. Target directories are never merged or overwritten.
4. **History validation is explicit.** Folder journals require valid version/type/phase fields, absolute normalized paths, same-parent moves, unique source/target/temp paths, and safe temporary names. Photo journals retain filename validation and reject unknown operation types. Older photo history without the new directory-ID field remains supported.
5. **Preview state survives formatting errors.** Clearing or temporarily mistyping the shared name leaves the captured sources available. Correcting the input restores the preview without accepting new source metadata. Refresh is available from either naming mode. Removing a nested selection rebuilds its snapshot.
6. **UI resource and usability cleanup.** Empty-state drawing disposes temporary fonts, the embedded logo is detached from its input stream, dark styling applies to already-created handles, sidebar buttons fit their panel, header borders match the dark theme, and F1/Ctrl+1/2/3 navigate the tabs.

## Automated verification

`test.ps1` passes **110 assertions** covering:

- Exact emoji prefixes, extension capitalization, natural sorting, both photo number positions, no-op stability, and a 1,000-photo multi-folder batch.
- Photo content and modification-time preservation; skipped videos, sidecars and nested photos; no history or lock artifacts in photo folders.
- Overlapping photo and folder names without overwrite; stale previews, occupied destinations, invalid names, duplicate and nested directory selections.
- Failure injection at both move phases, cancellation, recovery after interrupted rename and interrupted Undo, and finalizing a completed transaction.
- Folder suffixes `_01`, `_02`, `_110`, `_1119` and beyond; folder contents retain their names and contents.
- Same-path directory replacement despite matching photo metadata; history path manipulation; compatibility with earlier photo Undo.

## Practical boundaries

- Undo stores only the most recent successful batch. It restores names, not edited or deleted photo contents.
- File size and modification time detect ordinary photo changes; this is not a cryptographic content-integrity check.
- Another application, a disconnected drive, changed permissions or moved parent directories can prevent immediate restoration. The app retains recovery state and refuses to overwrite competing files or folders.
- New directory-ID verification depends on filesystem support for Windows file information. Network and removable filesystem behavior can differ from the local test drive.
- Long-path support also depends on Windows and the destination filesystem. The shipped config and manifest enable the app's support; exhaustive network-share and removable-drive testing was not performed.
- Folder-name mode rejects a parent and its nested folders in one batch. Photo mode does not recursively rename files or keep RAW/JPG/sidecar stems grouped.
- History stays under this Windows account's local application data. Moving the executable to another computer does not carry its Undo records.

## Desktop verification

The dark Photos, Folder names and Help pages were visually inspected in the running app, including the shared-name preview and the `_01` / `_02` sequence. Folder-name Rename/Undo, cancellation and recovery were exercised by automated tests on disposable directories with a separate test history directory. A desktop test harness references the shipped executable and supplies isolated sample folders. The user's existing Undo history was left intact. Manual end-to-end clicking of every control, exhaustive accessibility testing, and testing across every monitor scaling configuration were outside this audit's scope.
