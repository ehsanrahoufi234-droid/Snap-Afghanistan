# Snap Afghanistan — Stabilization Audit

This file tracks the stabilization work for the next desktop build.

## Audit priorities

1. Login and authentication flow
2. RTL layout consistency and responsive desktop sizing
3. Dashboard/navigation correctness
4. Member CRUD and attachment handling
5. Centers/sectors and subscription accounting
6. Backup/restore safety
7. Database integrity and migrations
8. Installer/build pipeline
9. Performance and startup behavior
10. Release smoke tests

## Initial findings

- The application is a native WPF desktop application and not a browser wrapper.
- Data is stored under `%LOCALAPPDATA%\\SnapAfghanistan\\Data`.
- SQLite is configured with foreign keys, WAL mode and a busy timeout.
- Authentication currently supports one administrator identity only; role-based users are not yet implemented.
- The login window is hard-coded as maximized with fixed minimum column widths, which can cause layout problems on smaller displays or unusual scaling.
- The current migration mechanism is column-existence based and sets `schema_version` to a fixed value after startup; it needs a versioned migration path before the data model grows further.
- The build workflow creates a Windows installer and runs a native self-test before packaging.

## Rule for stabilization

No destructive rewrite of the current main branch. Changes should be isolated, tested, and merged only after the installer build passes.
