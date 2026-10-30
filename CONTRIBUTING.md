# Contributing

Issues and pull requests are welcome. The MIT license allows anyone to use,
modify and redistribute Photo Renamer, including for commercial use.

## Development setup

Use Windows with .NET Framework 4.7.2 or later, Git, and Windows PowerShell or
PowerShell 7. No NuGet packages or IDE installation are required.

```powershell
git clone https://github.com/haldarsaurav/photo-renamer.git
cd photo-renamer
.\test.ps1
.\build.ps1
.\package.ps1
```

Build output is under `dist/`. Test output and sample folders stay under
`tests/` and are ignored by Git. Use disposable folders when testing changes;
never use your only copy of a photo collection.

## Before opening a pull request

- Run `test.ps1` and include relevant regression checks for rename, Undo,
  rollback or recovery changes.
- Run `build.ps1` and check the affected UI at your normal display scale.
- Stage the intended files and run `verify-public.ps1` to check for local
  paths, sensitive-looking strings and generated files in the Git index.
- Keep file operations free of overwrite and merge behavior. Preserve the
  recovery journal when restoration cannot finish.
- Keep the app offline and avoid adding telemetry or third-party dependencies
  without explaining the need.
- Use generic example folder names. Do not commit photo libraries, runtime
  history, credentials, personal paths or executable build products.

The existing tests use generated data and temporary per-run history folders.
Earlier history versions must stay readable when the journal format changes.

## Reporting a problem

Include your Windows and .NET versions, the operation used, a small example of
old and expected names, and the error message. Replace private folder names
and usernames with generic examples. If recovery is pending, keep the local
history and temporary names until the collection is restored; do not upload
them publicly.
