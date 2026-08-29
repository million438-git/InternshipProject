# 🚀 HUCEMS Enterprise Production Deployment Manual

This guide outlines the production deployment procedures for the **Hawassa Unified Campus Event Management System (HUCEMS)**.

---

## 🏗️ 1. QUICK DEPLOY WITH DOCKER & DOCKER COMPOSE (RECOMMENDED)

The fastest and most reliable way to run HUCEMS in production is using Docker Compose.

```bash
# 1. Clone or copy project to production server
cd HawassaUnifiedCampusEventManagementSystem

# 2. Create local secrets (do not commit .env)
cp .env.example .env
# Edit .env: MYSQL_ROOT_PASSWORD, DATABASE_CONNECTION_STRING, JWT_SECRET_KEY (32+ chars)

# 3. Start HUCEMS and MySQL 8.0 in isolated network
docker compose up -d --build

# 4. Verify running containers
docker compose ps
```

The system will be live and active at: `http://<your-server-ip>:5000`

---

## 🐧 2. DEPLOYMENT ON LINUX SERVER (UBUNTU / DEBIAN + NGINX)

### Step 1: Install .NET 10 Runtime
```bash
sudo apt-get update
sudo apt-get install -y dotnet-aspnetcore-runtime-10.0
```

### Step 2: Publish Production Bundle
```bash
dotnet publish -c Release -o /var/www/hucems
```

### Step 3: Create Systemd Service (`/etc/systemd/system/hucems.service`)
```ini
[Unit]
Description=Hawassa Unified Campus Event Management System
After=network.target mysql.service

[Service]
WorkingDirectory=/var/www/hucems
ExecStart=/usr/bin/dotnet /var/www/hucems/HawassaUnifiedCampusEventManagementSystem.dll --urls=http://127.0.0.1:5000
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=hucems-app
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable hucems.service
sudo systemctl start hucems.service
```

### Step 4: Configure Nginx Reverse Proxy (`/etc/nginx/sites-available/hucems`)
```nginx
server {
    listen 80;
    server_name campus.hawassa.edu.et;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/hucems /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

---

## 🪟 3. DEPLOYMENT ON WINDOWS SERVER / IIS

1. Open PowerShell as Administrator.
2. Run the automated deployment script:
   ```powershell
   .\deploy_production.ps1
   ```
3. Point your IIS Web Application root to the generated `.\publish` folder.
4. Ensure the **ASP.NET Core Hosting Bundle (v10.0)** is installed on the Windows Server.

---

## 🔐 4. PRODUCTION SECURITY BEST PRACTICES

1. **Environment Variables**: Never commit production passwords into version control. Supply `DATABASE_CONNECTION_STRING` and `JWT_SECRET_KEY` via server environment variables.
2. **HTTPS / SSL**: Install a Let's Encrypt SSL certificate using `certbot --nginx` on Linux or via IIS Certificate Manager on Windows.
3. **Database Snapshots**: SuperAdmin can download SQL snapshots from `/Admin/DatabaseManagement`. Restore is not executed in-app; apply a snapshot with MySQL tools on the server.
