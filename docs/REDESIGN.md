# Photo Renamer v1.1 design review

This redesign applies the complete **[Unslop skill](https://github.com/heydamir/unslop)**
by **Damir Sokolovsky ([@heydamir](https://x.com/heydamir))**, CC BY 4.0,
commit `4652254daff586ff4739849395a127b56f7f8cc7`. Its skill instructions,
signs, tokens, visual guidance, finished references and story renderer were
used. The workflow was adapted to native Windows Forms. The application is
still a Windows executable; the HTML story is a documentation presentation.

## Audit before editing

The actual v1.0 screen and the files that render it were reviewed against
Unslop's signs checklist. The issues spanned color, type, spacing, copy and
details, so the full nine-pass process was appropriate.

| Finding | Location | Change |
| --- | --- | --- |
| Saturated blue surfaces dominate the screen | `DarkTheme.cs` palette | A low-saturation graphite ramp |
| Purple and mint compete across unrelated controls | Primary buttons, selection, offline label, Help headings | Amber for sequence markers and selection; semantic green only for successful operations |
| Gradient tile, layered photo and colored badge compete at small sizes | `CreateIcon.cs` | One flat amber photo-and-sequence glyph |
| Repeated generic slogans consume the header and page titles | All three pages in `PhotoRenamer.cs` | Product name, task names and specific descriptions |
| Uppercase branding, labels, headings and privacy footer are noisy | Header, sidebar, tables and footer | Sentence case and quiet metadata |
| Unrelated gaps and heights break the rhythm | 26px edges, 15px sidebar padding, 37px rows, 41px buttons | 24px edges, 16px sidebar padding, 40px rows and controls |
| Bold text spreads emphasis across most controls | Buttons, navigation, labels and page headings | Regular and medium weights with size and color for hierarchy |
| Large outlined navigation buttons compete with the preview | Top navigation | Compact tabs with a restrained active underline |
| Proposed names do not distinguish the actual changing sequence | Photo and folder grids | Monospaced filenames with amber `_1` / `_01` markers |

## Direction

- **Product:** batch naming for personal photo collections.
- **Character:** calm, precise, archival.
- **World:** a photo archive and its numbered contact sheets.
- **Moment:** the real filename preview, with the changing number easy to spot.
- **Signature:** an amber underscore-and-number mark, also used in the app icon.

## Nine passes

1. **Spacing.** Both naming pages, Help, the sidebar and footer use the same
   4px rhythm. Page edges are 24px, panel padding 16px, preview rows 40px,
   and navigation controls 36px. Matching sidebar actions share dimensions.
2. **Color.** Background `#161617`, surface `#1c1c1d`, raised `#262628`, and
   hairlines `#2f2f31` form one neutral ramp. Primary text is `#edebe6`,
   secondary text `#b1b1b4`, metadata `#949499`, and the accent `#dfb66c`.
   The primary rename action is off-white with dark text. Errors and successful
   operations retain distinct semantic colors.
3. **Icons.** The gradient icon and green badge became a flat photo outline
   containing `_1`. It is generated at seven sizes and embedded in the app.
   There are no decorative emoji labels. Emoji in users' filenames remain
   data and are preserved, as required by the renamer.
4. **Copy.** The application header names the product. Pages say Rename photos,
   Rename folders, and Help & safety. Instructions describe the actual naming
   rules; short labels use sentence case. The footer gives a practical preview,
   Undo-storage and Help reminder.
5. **Type.** IBM Plex Sans Regular and Medium form the interface family, with
   IBM Plex Mono for filename strings, counts and sequence examples. These
   unmodified OFL fonts are embedded and registered privately for the process;
   no system font installation or runtime network request is involved.
6. **Layout.** The three tabs and their order are retained. A 280px folder
   sidebar and fluid preview use the desktop window's width. The compact
   header leaves more room for filenames. Help uses the available full width.
   Multiple selection, child-folder selection, pasted paths and drag/drop stay.
7. **Details.** Tabs use a quiet underline, controls have an 8px radius, and
   keyboard focus stays visible. Source names are secondary; target names are
   primary. The numeric component is amber when the full name fits. Long names
   retain ellipsis and full-name tooltips, including the proposed folder path.
8. **Visuals.** The actual before/after filenames are the product's visual
   material. They carry the accent. No generated scene, decorative chart or
   placeholder illustration is needed for this utility. No image prompt was used.
9. **Motion.** Native buttons use 140ms color feedback with
   `cubic-bezier(0.22, 1, 0.36, 1)`. Windows' menu-animation preference disables
   that feedback when requested. There are no idle loops or layout animations
   in the app. The separate story deliberately animates the design changes.

## Verification and result

- Built the portable native app and exercised it with generated JPEG/PNG files
  and a separate history directory.
- The existing 110 regression assertions passed, including numbering at both
  ends, multi-folder batches, suffix growth, collision handling, rollback,
  cancellation, Undo and interrupted recovery.
- Checked the Photos, Folder names and Help screens, then captured fresh
  documentation media from the actual application.
- Verified key contrast pairs: primary button 15.18:1, secondary text 7.96:1,
  muted metadata 5.64:1, and the amber marker on the raised surface 7.94:1.
- Reviewed the final design against the audit: neutral surfaces, one accent,
  consistent spacing and type, specific labels, flat icon and clear sequence.
  No decorative gradients, glows, badges, uppercase labels or invented metrics.
- Kept generic public examples and checked staged source for absolute personal
  paths, token patterns and generated runtime state.

The [story](redesign/story.html) groups the passes into four visible steps:
Spacing, Color and icons, Type and copy, and Details. It uses Unslop's original
renderer, in-place changes, redlines and text swaps, with its attribution
overlay. [MEDIA.md](MEDIA.md) distinguishes the reconstructed story from the
real-app walkthrough and explains how to regenerate both.

The naming engines are unchanged. The [safety audit](../AUDIT.md) still records
their practical limits. This design review does not claim exhaustive screen
reader, filesystem or monitor-scaling coverage.
