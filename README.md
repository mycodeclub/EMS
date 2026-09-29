# EMS — Employee Management System

ASP.NET Core MVC (.NET 10) with ASP.NET Core Identity and EF Core on PostgreSQL. By BitProSoftTech.

## Set up on a new machine

### 1. Install once

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (runs the local PostgreSQL)
- EF Core CLI: `dotnet tool install --global dotnet-ef`
- Trust the HTTPS development certificate: `dotnet dev-certs https --trust`

### 2. Start the database

From the repository root:

```sh
docker compose up -d
```

This starts PostgreSQL 17 in the container `ems-postgres` on `localhost:5432` (database `ems`, user `ems`, password `ems_dev_password`). Data is kept in the Docker volume `ems-pgdata`, and the container starts again with Docker Desktop.

### 3. Create the tables

```sh
cd EMS
dotnet ef database update
```

### 4. Run

```sh
dotnet run
```

Open https://localhost:7134 (http://localhost:5046 redirects to it).

On startup the app creates the `SuperAdmin` role and the super admin account below if they do not exist yet.

## Development login

| Role        | Email                  | Password          |
|-------------|------------------------|-------------------|
| Super admin | `superadmin@ems.local` | `SuperAdmin#2026` |

After signing in, the super admin lands on the admin console at `/Admin`: **Enquiries**, **Leads**, **Active customers** and **Billing**. These screens show sample data from `Services/Admin/SampleAdminData.cs`; they are not connected to the database yet.

Anyone can register a normal account from the Register page. It has no admin access.

> These credentials are for local development only. The password lives in `appsettings.Development.json`. In any other environment set `SuperAdmin__Email` and `SuperAdmin__Password` (and `ConnectionStrings__DefaultConnection`) as environment variables. An existing account keeps its password; changing the setting later does not reset it.

## Everyday commands

Run these from the `EMS` folder.

| Task | Command |
|------|---------|
| Add a migration after changing a model | `dotnet ef migrations add <Name> -o Data/Migrations` |
| Apply migrations | `dotnet ef database update` |
| Check for model changes without a migration | `dotnet ef migrations has-pending-model-changes` |
| Open a SQL prompt | `docker exec -it ems-postgres psql -U ems -d ems` |
| Wipe the database and start over | `docker compose down -v`, `docker compose up -d`, then `dotnet ef database update` |

Table and column names are case-sensitive in PostgreSQL, so quote them: `select * from "Enquiries";`

## Configuration

| Setting | Where | Purpose |
|---------|-------|---------|
| `ConnectionStrings:DefaultConnection` | `appsettings.json` | PostgreSQL connection |
| `SuperAdmin:Email` / `SuperAdmin:Password` | `appsettings.json` / `appsettings.Development.json` | Account seeded at startup |
| `Company` | `appsettings.json` | Company details shown on the landing page |

## Troubleshooting

- **`address already in use` on port 7134 or 5046**: another copy of the app is running. Stop it (Ctrl+C in its terminal), or find it with `netstat -ano | findstr :7134` and end that process.
- **`Failed to connect to 127.0.0.1:5432`**: the database is not running. Start Docker Desktop, then `docker compose up -d`.
- **`relation "..." does not exist`**: the tables are missing or out of date. Run `dotnet ef database update`.
