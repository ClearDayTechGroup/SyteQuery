# SyteQuery

**A native Windows query tool for Infor SyteLine** — an SSMS-style workspace for exploring and querying your SyteLine database, built on SyteLine's own REST API.

<!-- Add a screenshot or short demo GIF here: docs/images/hero.png -->

Browse every table, view, stored procedure and function across your environments, write T-SQL with IntelliSense, run it, and export the results — without leaving a single window, and without the round trips through reports, forms and one-off scripts that ad-hoc SyteLine data work usually involves.

> **Status: early release (v0.1).** It is used day to day against real SyteLine environments, but it is young software. Read [Safety](#safety) before pointing it at production.

## Features

**Explore**
- **Object Explorer** for every configured environment: tables, views, stored procedures and functions grouped by schema, with columns, keys and triggers loaded as you expand. Search, refresh, and a background load of the object lists so folders open instantly.
- **Object actions**: view a stored procedure's, function's or trigger's definition; script a custom (`ue_`) table; quick-select the top 100 rows; or **compare a stored procedure across two environments** with a side-by-side diff.

**Query**
- **Multi-tab editor** (AvalonEdit) with T-SQL syntax highlighting, **IntelliSense as you type** (keywords, tables, views, procedures, functions, snippets, and columns after `table.`), format-query, and comment/uncomment.
- Query **analysis before it runs**: warns about missing `WHERE`, `SELECT *`, possible Cartesian products and more, and applies a `TOP 1000` limit when you haven't set one.
- **Failures are impossible to miss**: a red banner appears right above the editor with the SQL Server error.
- **Query history** (bounded, searchable) and **snippets** (save, organise, reload).

**Results**
- A native, **virtualized grid** that stays smooth on large result sets, with quick search across all columns and export to **Excel, CSV and JSON**.

**Built for daily use**
- **Multiple environments**, each with its own credentials, switchable per query tab.
- Credentials are encrypted with Windows DPAPI (per user, per machine) — nothing is stored in plain text and no keys live in the app or its config.
- Windows 11 look with **light / dark / follow-Windows** themes, an installer, and in-app update checks.
- Everything is stored locally in a SQLite database under `%LOCALAPPDATA%\SyteQuery`. No account, no sign-in, no cloud service, no telemetry.

<!-- Screenshots: docs/images/object-explorer.png, query-editor.png, compare.png -->

## How it works

SyteQuery talks to SyteLine through Infor's documented **IDO REST v2 API** (`/IDORequestService/ido/...`) — no proprietary client libraries. It authenticates with the environment's own security token endpoint and calls IDO methods over HTTPS.

To run SQL it uses one small custom IDO, **`ue_RC_QueryTool`**, installed on each SyteLine environment: its `ExecuteQuery` method runs the command you send and returns the rows as JSON. When you add an environment, SyteQuery checks that the IDO is present. The source for this IDO will be published in this repository so you can review exactly what runs on your server before installing it.

Metadata (the object lists, columns, triggers) is read from SQL Server's catalog views through the same IDO and cached in memory per environment.

## Requirements

- Windows 10 or 11 (x64)
- An Infor SyteLine / CloudSuite Industrial environment with the IDO REST service reachable from your PC
- A SyteLine user with permission to call IDOs (and the `ue_RC_QueryTool` IDO installed — see above)

## Install

Download the latest `ClearDay.SyteQuery-win-Setup.exe` from [Releases](../../releases) and run it. It installs per user (no administrator rights needed) and the .NET runtime is included. The installer isn't code-signed yet, so Windows SmartScreen may show an "unknown publisher" warning — choose *More info → Run anyway*.

Then: **Tools → Environments → Add**, enter your environment's URL (just the scheme and host, e.g. `https://csi10x.erpsl.inforcloudsuite.com`), the SyteLine **configuration** name, and your credentials.

## Build from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) on Windows.

```powershell
git clone https://github.com/<owner>/SyteQuery.git
cd SyteQuery
dotnet build
dotnet run --project SyteQuery.Desktop
```

To build the installer: `./build/Build-Installer.ps1 -Version 0.1.0`. See [CONTRIBUTING.md](CONTRIBUTING.md) for the project layout and workflow.

## Safety

SyteQuery runs the SQL you type, with the permissions of the SyteLine account you connect with.

- It **blocks** `CREATE` / `ALTER` / `DROP` of procedures, views, functions and triggers (other than `extgen` procedures), and applies a row limit to unbounded `SELECT`s.
- It does **not** stop `INSERT`, `UPDATE` or `DELETE` statements — if the account can change data, so can your query. **Use a read-only account** for exploration, and be especially careful with production.
- Always review a query before pressing Execute (F5).

Found a security problem? Please follow [SECURITY.md](SECURITY.md).

## Built by ClearDay Tech Group

SyteQuery is built and maintained by [ClearDay Tech Group](https://cleardaytechgroup.com), a consultancy that builds tooling, integrations and automation for Infor SyteLine environments. If you need custom SyteLine development, IDO and integration work, reporting, or help getting more out of your system, we'd like to hear from you.

## License

[MIT](LICENSE) © 2026 ClearDay Tech Group. Third-party components and their licenses are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). SyteQuery is an independent project and is not affiliated with or endorsed by Infor; "Infor" and "SyteLine" are trademarks of their respective owners.
