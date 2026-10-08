# Contributing to SyteQuery

Thanks for your interest! Bug reports, ideas and pull requests are all welcome.

## Before you start

- **Bugs and small fixes**: open an issue (or go straight to a PR for something obvious).
- **New features or anything sizeable**: please open an issue first so we can agree on the approach before you spend time on it.
- **Security problems**: don't open a public issue — see [SECURITY.md](SECURITY.md).

## Suggesting a feature

Use the **Feature request** form when you [open an issue](../../issues/new/choose). Search existing requests first and add your use case to a matching one instead of opening a duplicate. One idea per issue, and describe the problem you're trying to solve before the solution you have in mind. Known gaps (for example, no transactions held open across runs) are listed under *Known limitations* in the [README](README.md).

## Building

You need Windows and the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
dotnet tool restore                          # dotnet-ef and vpk, pinned in .config/dotnet-tools.json
dotnet build SyteQuery.Desktop               # the app (and Core). Not the whole solution - see below
dotnet test SyteQuery.Tests
dotnet run --project SyteQuery.Desktop
```

Build the app project, not the whole solution: the solution also contains `SyteQuery.IDO`, which compiles against Infor's SyteLine assemblies and needs their location (`InforBinPath`, see `SyteQuery.IDO/README.md`). You only need it if you are working on the IDO.

On first run the app creates its SQLite database at `%LOCALAPPDATA%\SyteQuery\app.db`. To try it against a real system you need a SyteLine environment — see the README's *How it works* section. There is no test environment bundled with the repo.

## Project layout

| Folder | What's in it |
|---|---|
| `SyteQuery.Core` | UI-free class library: the IDO REST client, environment/session handling, metadata cache and preloader, query analysis and validation, history, snippets, export, EF Core + SQLite storage and migrations. Organised by feature under `Features/`. |
| `SyteQuery.Desktop` | The WPF application: windows, view models and views (`ObjectExplorer`, `QueryEditor`, `Results`, `Snippets`, `History`, `Environments`, `Compare`), theming, and the Velopack entry point. |
| `SyteQuery.Tests` | xUnit tests for the Core logic. Also compiles `SyteQuery.IDO/ResultSetJson.cs` (plain C#, no SyteLine types) so the IDO's serializer is tested without Infor's assemblies. |
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
2. Make sure `dotnet build SyteQuery.Desktop` is clean (no new warnings), `dotnet test SyteQuery.Tests` passes, and the app starts.
3. Describe *what* and *why*, and how you tested it. For UI changes include a screenshot (light and dark if it affects both).
4. If you add or update a NuGet package, regenerate `THIRD_PARTY_NOTICES.md` with `./build/Generate-ThirdPartyNotices.ps1`.

GitHub runs the build and the tests on every pull request. The tests cover the query IDO's result serializer, reading the IDO's output, running scripts in batches, statement analysis and the row limit, `GO` handling, the DDL block and Excel export. Add a test with any change to that logic. There are no tests yet for the metadata cache, the error detector or the WPF views and view models; adding them would be a welcome contribution.

By contributing you agree that your work is released under the project's [MIT license](LICENSE).
