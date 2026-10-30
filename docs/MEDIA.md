# App screenshots and walkthrough

The images in `images/` are captures of the actual Photo Renamer release UI,
not mockups or AI-generated interface images. They use generated sample JPEG
and PNG files in two generic folders. No personal photos, account details or
absolute folder paths are shown.

The GIF is a paced sequence of real app captures. Pauses and short transitions
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
committed. Media is provided under the repository's MIT license.
