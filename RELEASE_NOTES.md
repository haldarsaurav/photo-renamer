# Photo Renamer v1.1.0

The Unslop redesign of the portable Windows photo and folder renamer.

- A graphite workspace with consistent spacing and quieter navigation.
- Bundled IBM Plex Sans and Mono fonts, loaded privately without installation.
- Amber highlights identify the incremental number in photo and folder previews.
- A flat photo-and-sequence icon, specific labels, and clear keyboard focus.
- Subtle hover feedback follows Windows' menu-animation preference.
- Updated actual-app screenshots, animated walkthrough, and a credited Unslop
  design story with the full audit and nine-pass report.
- All existing naming, preview, Undo, cancellation and recovery behavior remains
  covered by the 110-assertion regression suite.

Download `Photo-Renamer.zip`, extract it, and run `Photo Renamer.exe`. Keep the
config beside the executable. Requires Windows with .NET Framework 4.7.2 or
later. The executable is unsigned. `SHA256SUMS.txt` records the ZIP checksum.
IBM Plex fonts use SIL OFL 1.1; font and third-party notices are included.

---

## v1.0.0

First public release of the portable Windows photo and folder renamer.

- Rename photos from their containing folder's name with the number at the
  start or end.
- Rename several folders using one shared name and suffixes such as `_01`,
  `_02`, `_110` and `_1119`.
- Preview every change before confirming; keep emoji and file extensions.
- Use batch Undo, cancellation with restoration, and interrupted-batch recovery.
- Dark interface with built-in Help & safety and keyboard navigation.
- Generic defaults and examples; no personal photo paths or runtime data in
  the repository or release.
- MIT licensed for personal, commercial and community use.

Download `Photo-Renamer.zip`, extract it, and run `Photo Renamer.exe`. Keep the
included config beside the executable. Requires Windows with .NET Framework
4.7.2 or later. The executable is unsigned. `SHA256SUMS.txt` records the ZIP's
checksum. Source and build instructions are in the repository.
