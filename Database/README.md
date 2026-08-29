# HUCEMS MySQL 8 database

This folder holds the schema used by the Hawassa Unified Campus Event Management System.

| File | Purpose |
| :--- | :--- |
| **`university_event_management.sql`** | MySQL 8 schema, keys, indexes, and baseline seed (roles, permissions, faculties, departments, venues, event categories, system settings, SuperAdmin). |

There is no `migrations_user_relationships.sql` in this repository. `user_relationships` is not part of the runtime model.

---

## Connection string

The committed `appsettings.json` does not include a database password. Local development uses `appsettings.Development.json` (`ConnectionStrings:DefaultConnection`). Production and Docker use `DATABASE_CONNECTION_STRING` (see `.env.example` at the repo root).

---

## Initialize the database

**PowerShell (adjust the MySQL path and password to your machine):**

```powershell
& "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -u root -p < "Database\university_event_management.sql"
```

**MySQL Workbench:** File → Open SQL Script → `Database/university_event_management.sql` → Execute.

Docker Compose mounts this file into MySQL as `/docker-entrypoint-initdb.d/init.sql` on first volume create.

---

## Seed SuperAdmin

After init, sign in at `http://localhost:5110/Account/Login`.

- Username: `superadmin`
- Email: `superadmin@hawassa.edu.et`
- Password: `SuperAdmin@2026!` (legacy SHA-256 hash in the seed; login rehashes to PBKDF2)

---

## In-app database vault

SuperAdmin-only: `/Admin/DatabaseManagement` can download SQL snapshots. Restore is not run inside the app; apply a dump with MySQL tools. Row-level CRUD is SuperAdmin-only at `/Admin/DatabaseRecords`.
