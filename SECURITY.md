# Security Policy

SyteQuery handles credentials for ERP systems and runs SQL against them, so security reports are taken seriously.

## Reporting a vulnerability

**Please don't open a public issue.** Use GitHub's private reporting instead: on this repository go to **Security → Report a vulnerability**. Include what you found, how to reproduce it, and the version (Help → About) you tested.

You can expect an acknowledgement within a few days and a fix or mitigation plan as soon as it's understood. Please give us a reasonable chance to release a fix before disclosing publicly; we're happy to credit you in the release notes.

## Supported versions

Only the latest release receives security fixes.

## How SyteQuery handles sensitive data

- **Credentials** for each environment are encrypted at rest with ASP.NET Core Data Protection (keys generated per machine and protected by Windows DPAPI for your user account). They are decrypted in memory to authenticate and are not written to logs.
- **No telemetry.** The app only talks to the SyteLine environments you configure (and, if you use *Check for Updates*, GitHub Releases).
- **Local storage** is a SQLite file under `%LOCALAPPDATA%\SyteQuery`. It contains your environments (with encrypted passwords), query history and snippets. Query history stores the SQL text you ran — treat the file accordingly.
- **Queries run with the permissions of the account you connect with.** SyteQuery blocks `CREATE/ALTER/DROP` of procedures, views, functions and triggers, but it does not block data-changing statements. Use a least-privilege (ideally read-only) SyteLine account.

## Out of scope

Issues that require an attacker who already has your Windows account or physical access to your unlocked machine, and problems in SyteLine/Infor software itself.
