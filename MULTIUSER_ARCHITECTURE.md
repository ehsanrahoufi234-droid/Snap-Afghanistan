# Snap Afghanistan 1.4 — Multi-user LAN Architecture

## Goal
Provide a real multi-user desktop system for a small office without requiring Internet access. Two or more Windows PCs on the same trusted LAN/Wi-Fi must work against one authoritative dataset, with separate app users, roles, sessions, audit history and safe concurrent writes.

## Non-negotiable constraints

- Never place `snap.db` on a Windows shared folder / UNC path.
- Never let two PCs open the same SQLite file directly over the network.
- Never maintain independent writable databases on client PCs and try to merge them later.
- Authorization is enforced on the server, not only by hiding UI buttons.
- The server is the only process allowed to own authoritative SQLite writes in network mode.
- If the server is unreachable, clients fail closed and do not create a local divergent database.
- Existing 1.3 data must be migratable without re-entering members, centers, payments or attachments.

## Target topology

```text
Windows PC 1 (Host)
  Snap Server Host
    - authoritative SQLite database
    - attachments
    - automatic backups
    - authentication / authorization
    - audit log
    - LAN endpoint
        |
        | encrypted authenticated LAN channel
        |
  +-----+--------------------+
  |                          |
Snap Desktop Client      Snap Desktop Client
Host PC                  PC 2 / PC 3
```

The desktop UI remains native WPF. The host and remote PCs use the same desktop application. In host mode the desktop client connects to the server through loopback; remote clients connect through the LAN address.

## Storage ownership

Authoritative server data belongs under a machine-scoped data root so a background host process can access it regardless of which Windows user is logged in:

```text
%ProgramData%\SnapAfghanistan\
  Data\snap.db
  Data\attachments\
  Data\backups\
  Logs\
  Server\
```

Existing `%LOCALAPPDATA%\SnapAfghanistan` data is migrated once when enabling host mode, after a verified pre-migration backup. Migration is never destructive until integrity checks pass.

## Transport

The network layer must provide confidentiality and server identity on the LAN. The implementation uses a dedicated encrypted endpoint and certificate pinning/pairing; plaintext database access is forbidden. A host exposes one configurable TCP port only on Private networks. The client stores the approved server identity and refuses an unexpected certificate/fingerprint change.

## Authentication and sessions

App users are stored centrally in the server database. Passwords are never stored as plaintext. Password hashing uses PBKDF2-SHA256 with per-user random salt and a high iteration count. Login creates a random expiring server session token. Sessions are revocable and include user id, role, machine name, creation time, last activity and expiry.

Default roles:

- `manager`: full operational access, user management, settings, backup/restore, destructive corrections.
- `accountant`: members/centers read, subscriptions/payments create and correct, reports; no user administration or permanent purge.
- `staff`: member/center registration and normal edits, notes and read-only subscription visibility; no financial correction/delete, restore, settings or user management.

Permissions are represented explicitly so roles can evolve without scattering string checks through UI files.

## Concurrency

Every mutable business row retains a `version` integer. Update commands include the expected version. If another user changed a record after it was opened, the server rejects the stale update and asks the client to refresh instead of silently overwriting another user's work.

Financial operations run inside SQLite transactions on the host. Payment correction/delete continues to rebuild the due-date chain atomically. One server process serializes authoritative writes and SQLite WAL remains local to the host machine.

## Audit

Every create/update/archive/delete/restore/payment correction records:

- app user id and display name
- role
- client machine name
- entity type/id
- action
- timestamp
- concise before/after or operation detail where practical

Audit information is server-generated. A client cannot choose its own actor identity.

## Attachments

Tazkira and other member attachments remain files, not BLOBs in SQLite. They live only on the host under the authoritative attachment directory. Remote clients upload/download through the authenticated server. Attachment paths from the host filesystem are never exposed as usable client paths.

## Backup / restore

Automatic backup runs only on the host and includes database + attachments + manifest + SHA-256 as in 1.3. Restore is manager-only, creates a pre-restore emergency backup and temporarily rejects new writes while restore is in progress. Clients are forced to refresh after a successful restore.

## Offline behavior

The application is Internet-independent but not server-independent in multi-user mode. LAN clients require the host to be available. If the host is offline, the client shows `سرور دفتر در دسترس نیست` and does not open a writable fallback database. This avoids split-brain data.

## Upgrade path

Phase 1: central users/roles/sessions and server ownership boundary.
Phase 2: LAN transport + remote gateway for all existing repository operations and attachments.
Phase 3: optimistic concurrency, richer audit, admin user UI, host/client setup wizard, migration and installer integration.
Phase 4: Windows CI smoke test starts the host, logs in two simulated clients, performs competing CRUD/payment operations, validates conflict handling, backup, restart and reconnect.

No multi-user changes are merged to `main` until the full Windows build, server/client smoke test and installer packaging pass.
