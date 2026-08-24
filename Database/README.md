# 🗄️ HUCEMS MySQL 8 Database Architecture & Setup Guide

This directory contains the complete database scripts and schema definitions for the **Hawassa Unified Campus Event Management System (HUCEMS)**.

---

## 📁 Directory Structure

| File | Purpose |
| :--- | :--- |
| **`university_event_management.sql`** | Complete MySQL 8 database schema (41 tables), foreign keys, indexes, and clean baseline configuration (Roles, Permissions, Faculties, Departments, Venues, Event Categories, System Settings, and Master SuperAdmin). |
| **`migrations_user_relationships.sql`** | Migration script for student & organizer connection/follow graph (`user_relationships`). |

---

## ⚙️ Database Configuration

### 1. Connection String (`appsettings.json`)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=university_event_management;User=root;Password=@root;"
  }
}
```

---

## 🚀 How to Initialize the Database

### Option A: Via PowerShell / Windows Command Line
```powershell
& "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -u root -p"@root" < "Database\university_event_management.sql"
```

### Option B: Via MySQL Workbench
1. Open **MySQL Workbench** and connect to your local MySQL server (`localhost:3306`).
2. Go to **File -> Open SQL Script...** and select `Database/university_event_management.sql`.
3. Click the ⚡ **Execute** button to run the entire script.

---

## 🔑 Initial Master Administrator Account

After initializing the database, log in with the root SuperAdmin account:

* **Portal URL:** [`http://localhost:5110/Account/Login`](http://localhost:5110/Account/Login)
* **Username:** `superadmin`
* **Email:** `superadmin@hawassa.edu.et`
* **Password:** `SuperAdmin@2026!` *(or `Admin@2026`)*
* **Role:** `SUPERADMIN` (Full System Clearance & Disaster Vault Access)

---

## 🛡️ Built-in Database Disaster Recovery & SQL Exporter

HUCEMS includes an in-app Database Management Vault available to SuperAdmins at:
👉 **[`http://localhost:5110/Admin/DatabaseManagement`](http://localhost:5110/Admin/DatabaseManagement)**

Features:
- **Live SQL Snapshot:** Generates raw `.sql` backups of 12 core relational tables with 1-click.
- **Backup Download & Deletion:** Inspect, download, or safely delete historical backups.
- **Interactive CRUD Result Grid:** Real-time database inspection and row-level editing at [`/Admin/DatabaseRecords`](http://localhost:5110/Admin/DatabaseRecords).
