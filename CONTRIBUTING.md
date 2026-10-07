# Contributing to SyteQuery

Thanks for your interest! Bug reports, ideas and pull requests are all welcome.

## Before you start

- **Bugs and small fixes**: open an issue (or go straight to a PR for something obvious).
- **New features or anything sizeable**: please open an issue first so we can agree on the approach before you spend time on it.
- **Security problems**: don't open a public issue — see [SECURITY.md](SECURITY.md).

## Suggesting a feature

Use the **Feature request** form when you [open an issue](../../issues/new/choose). Search existing requests first and add your use case to a matching one instead of opening a duplicate. One idea per issue, and describe the problem you're trying to solve before the solution you have in mind. Known gaps (one `SELECT` per run, no transactions held open across runs) are listed under *Known limitations* in the [README](README.md).

## Building

You need Windows and the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
dotnet tool restore        # dotnet-ef and vpk, pinned in .config/dotnet-tools.json
dotnet build
dotnet run --project SyteQuery.Desktop
```

On first run the app creates its SQLite database at `%LOCALAPPDATA%\SyteQuery\app.db`. To try it against a real system you need a SyteLine environment — see the README's *How it works* section. There is no test environment bundled with the repo.

## Project layout

| Folder | What's in it |
|---|---|
| `SyteQuery.Core` | UI-free class library: the IDO REST client, environment/session handling, metadata cache and preloader, query analysis and validation, history, snippets, export, EF Core + SQLite storage and migrations. Organised by feature under `Features/`. |
| `SyteQuery.Desktop` | The WPF application: windows, view models and views (`ObjectExplorer`, `QueryEditor`, `Results`, `Snippets`, `History`, `Environments`, `Compare`), theming, and the Velopack entry point. |
| `SyteQuery.IDO` | The SyteLine-side query IDO (a .NET Framework 4.7.2 class) with its build and install guide. It is built separately from the app (the GitHub build skips it) and needs Infor's SyteLine SDK assemblies, which can't be committed — see `SyteQuery.IDO/README.md`. |
| `build` | `Build-Installer.ps1` (installer), `Generate-ThirdPartyNotices.ps1`, `Scan-ForSecrets.ps1`. |

Rough rule: anything that doesn't need WPF belongs in `SyteQuery.Core`.

## Conventions

- Match the surrounding code — naming, comment density and idiom. Comments should explain *why*, not restate the code.
- UI follows MVVM-ish lines: view models raise `INotifyPropertyChanged`; code-behind handles things that aren't naturally bindable (AvalonEdit, dialogs, dynamic grid columns).
- Use the Fluent theme brushes (`TextFillColorPrimaryBrush`, `LayerFillColorDefaultBrush`, …) rather than hard-coded colors so both light and dark modes work. Check your change in **both**.
- Never commit credentials, tokens, tenant URLs or customer data — including in tests, screenshots and issue text.

## Database changes

Storage is EF Core with SQLite. After changing an entity or `ApplicationDbContext`:

```powershell
dotnet ef migrations add <Name> --project SyteQuery.Core
```

and commit the generated migration. Existing users' databases are migrated automatically at startup, so migrations must be safe on a populated database.

## Pull requests

1. Branch from `main`; keep the PR focused on one thing.
2. Make sure `dotnet build` is clean (no new warnings) and the app starts.
3. Describe *what* and *why*, and how you tested it. For UI changes include a screenshot (light and dark if it affects both).
4. If you add or update a NuGet package, regenerate `THIRD_PARTY_NOTICES.md` with `./build/Generate-ThirdPartyNotices.ps1`.

There is no automated test suite yet. Adding one — especially for query analysis/validation, the error detector and the metadata cache — would be a very welcome contribution.

By contributing you agree that your work is released under the project's [MIT license](LICENSE).
