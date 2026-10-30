# App screenshots and walkthrough

The screenshots and `walkthrough.gif` in `images/` capture the actual v1.1 UI,
not mockups or AI-generated interface images. They use generated sample JPEG
and PNG files in two generic folders. No personal photos, account details or
absolute folder paths are shown.

The walkthrough GIF is a paced sequence of real app captures. Pauses and short transitions
are edited for readability. The two actual confirmation dialogs are enlarged
on a plain background rather than composited into an invented window layout.
The photo rename and Undo steps were performed on the generated sample files.

## Files

- `photos.png`: photo preview with numbers at the end.
- `number-at-start.png`: the same sample files with numbers at the start.
- `folders.png`: shared folder-name preview with `_01` / `_02` suffixes.
- `help.png`: the built-in Help & safety tab.
- `walkthrough.gif`: preview, placement selection, confirmation, rename, Undo,
  folder naming and Help.
- `redesign.mp4` and `redesign.gif`: a separate HTML reconstruction of the
  interface changing through four grouped design steps. These are a design
  presentation, not a screen recording of the Windows application.

## Unslop story

The redesign used the complete [Unslop skill](https://github.com/heydamir/unslop)
by Damir Sokolovsky ([@heydamir](https://x.com/heydamir)), CC BY 4.0, at commit
`4652254daff586ff4739849395a127b56f7f8cc7`, including its reference sheets,
nine-pass process and original `story.mjs` renderer. The video carries the
renderer credit. [REDESIGN.md](REDESIGN.md) records the audit and result.

`redesign/story.html` is the editable presentation. It preserves the native
app's screen sections and uses generic sample names. `redesign/icon-before.png`
is the original Photo Renamer icon retained for the before state. The story
groups the nine passes into Spacing, Color and icons, Type and copy, and
Details, with in-place changes, text swaps, redlines and corner measurements.

On Windows, with Node, npm, Git, PowerShell and Chrome or Edge installed:

```powershell
.\tools\render-redesign.ps1 -Setup
```

This fetches the full pinned skill into ignored `.build/vendor/unslop` and
installs pinned Playwright Core and ffmpeg dependencies there. Subsequent
renders use `.\tools\render-redesign.ps1`. The generated self-contained story
and intermediate frames stay inside `.build`. This tooling is for maintaining
documentation; the app needs none of it.

## Refresh the media

1. Build the current app with `build.ps1`.
2. Run `tools/start-media-demo.ps1`, then launch
   `dist/PhotoRenamer.MediaDemo.exe`. This hosts the real app with generated
   sample photos and an isolated history directory. It does not use the
   normal account's Undo history.
3. Capture only the demo application's window and its own dialogs. Store raw
   captures in the ignored `.build/media-frames/` directory using the filenames
   listed in `tools/make-doc-media.py`.
4. Review every capture for desktop overlays, personal paths and other data.
5. Run `python tools/make-doc-media.py` with Pillow installed. Pillow is only
   required to maintain the media, not to build or use Photo Renamer.

Raw captures, sample libraries, test history and demo executables are excluded
from Git. Only reviewed documentation images and generic helper source are
committed. Actual-app media is provided under the repository's MIT license;
the credited Unslop story retains its CC BY 4.0 attribution. IBM Plex fonts
retain SIL OFL 1.1. See [third-party notices](../THIRD_PARTY_NOTICES.md).
