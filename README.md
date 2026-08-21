# Snap Afghanistan Desktop 1.4

Snap Afghanistan is a native Windows desktop management system for members, partner centers, sectors, monthly subscriptions, payments, reports, notes, archives, and backups.

## Platform

- Native C# / WPF desktop application
- .NET Framework 4.8, x64
- Windows 10/11
- SQLite database owned by the main office computer (Server)
- Offline LAN/Wi-Fi client/server operation; Internet is not required for core use

## Multi-user architecture

Version 1.4 replaces the old single-user-only operating model with real office users and sessions:

- Administrator, accountant, and employee roles
- Per-user permission overrides
- Independent usernames and passwords
- PBKDF2-SHA256 password hashing with per-user salts
- Session-aware audit identity and machine name
- Protection against disabling or demoting the last active administrator
- Optimistic version checks to prevent silent overwrites when two users edit the same record

The Server computer owns the database and attachments. Client computers communicate with it over the local network through the Snap LAN service. The SQLite file is never opened directly through a Windows shared folder.

## Members and documents

- Member CRUD, archive, trash, and restore
- Fast member search and pagination
- Quick member details panel
- Tazkira attachment preview for JPG/PNG
- Open/save-copy support for source documents including PDF
- Attachments are stored outside the SQLite file under the controlled data directory

## Centers, sectors, and subscriptions

- Center and sector CRUD, archive, trash, and restore
- Monthly subscription setup with Solar Hijri dates as the primary UI dates
- Payment registration, editing, and deletion
- Payment-chain and next-due-date rebuild after corrections
- Clickable subscription-health indicators
- Revenue trend for the latest six Solar Hijri months

If a center is moved to the trash, its payment history is kept so the center can be restored, but those payments are excluded from current revenue cards and the active revenue chart while the center remains deleted. Restoring the center restores its historical contribution. Permanent deletion removes the center payment rows.

## Backup and recovery

Backups contain the central SQLite database, member attachments, a manifest, and a database SHA-256 digest. Restore creates an emergency backup first and validates the replacement before completing. In LAN mode, backup and restore are intentionally available only on the Server computer.

Normal data path on the Server:

`%LOCALAPPDATA%\SnapAfghanistan\Data`

## Build validation

The Windows CI pipeline performs:

1. package restore
2. Release x64 build
3. native database/CRUD/payment/archive self-test
4. deleted-center revenue visibility regression test
5. authenticated loopback LAN server/client smoke test
6. Inno Setup compilation
7. installer artifact upload

Installer output:

`SnapAfghanistan-Setup-1.4.0.exe`
