# Snap Afghanistan — Stabilization Audit

## v1.2 stabilization status

- [x] Native WPF desktop architecture retained; no browser wrapper.
- [x] Login layout made responsive for Windows scaling and smaller displays.
- [x] Main shell RTL hierarchy corrected: titles first from the right, icons to the left.
- [x] Logo/title order normalized in the right navigation.
- [x] Member CRUD, archive, soft-delete, restore, attachment and individual PDF paths audited.
- [x] Sector/center CRUD and subscription/payment flows audited.
- [x] Reports for members, centers, sectors, debtors and payments audited.
- [x] SQLite uses foreign keys, WAL, busy timeout and schema version 4.
- [x] Database quick integrity-check helper added.
- [x] Backup contains database + attachments + manifest + database SHA-256.
- [x] Restore validates the backup, creates an emergency backup first and rolls back automatically on failure.
- [x] ZIP restore blocks unsafe path traversal and enforces extracted-size limits.
- [x] Self-test can use an isolated data root and exercises core CRUD, reports, PDF and backup.
- [x] Windows CI uses isolated self-test data before packaging.
- [x] Application, installer and artifact version bumped to 1.2.0.

## Deliberate limitation

v1.2 remains a single-computer local SQLite application. A SQLite database file must not be shared directly over a normal network folder for simultaneous multi-computer use. Proper two-computer/multi-user support requires a local service/server layer and is a separate architecture change.

## Release rule

The stabilization branch is merged only after the Windows build, native self-test and Setup.exe packaging all pass.
