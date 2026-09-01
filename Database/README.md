# HUCEMS MySQL Database Architecture & Setup Guide
 
This directory contains the database scripts and schema definitions for the Hawassa Unified Campus Event Management System (HUCEMS).
 
---
 
## Directory Structure
 
| File | Purpose |
| :--- | :--- |
| **`university_event_management.sql`** | Complete MySQL 8 database schema (34 relational tables), foreign keys, indexes, and baseline seed data (Roles, Permissions, Faculties, Departments, Venues, Event Categories, System Settings, and Master SuperAdmin). |
 
---
 
## Database Configuration
 
### Connection String (`appsettings.json`)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=university_event_management;User=root;Password=@root;"
  }
}
```
 
---
 
## Initializing the Database
 
### Option A: Via PowerShell / Windows Command Line
```powershell
& "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -u root -p"@root" < "Database\university_event_management.sql"
```
 
### Option B: Via MySQL Workbench
1. Open **MySQL Workbench** and connect to your local MySQL server (`localhost:3306`).
2. Go to **File -> Open SQL Script...** and select `Database/university_event_management.sql`.
3. Click the **Execute** button to run the script.
 
---
 
## Master Administrator Account
 
After initializing the database, sign in with the initial administrative account:
 
* **Portal URL:** `http://localhost:5110/Account/Login`
* **Username:** `superadmin`
* **Email:** `superadmin@hawassa.edu.et`
* **Password:** `SuperAdmin@2026!`
* **Role:** `SUPERADMIN`
 
---
 
## Database Management & Backup
 
SuperAdmins can access the Database Management interface at:
`http://localhost:5110/Admin/DatabaseManagement`
 
Features:
- Database snapshot generation and export
- Backup download and maintenance
- Table records inspection at `/Admin/DatabaseRecords`
