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

On startup the app creates the `SuperAdmin` and `OrgAdmin` roles and the super admin account below if they do not exist yet.

## Development login

| Role        | Email                  | Password          |
|-------------|------------------------|-------------------|
| Super admin | `superadmin@ems.local` | `SuperAdmin#2026` |

> These credentials are for local development only. The password lives in `appsettings.Development.json`. In any other environment set `SuperAdmin__Email` and `SuperAdmin__Password` (and `ConnectionStrings__DefaultConnection`) as environment variables. An existing account keeps its password; changing the setting later does not reset it.

After signing in, the super admin lands on the admin console at `/Admin`: a dashboard with charts, **Enquiries**, **Leads**, **Customers & trials** and **Billing**. Enquiries (from the website contact form) and free trials come from the database. Leads, paying customers and billing are not stored yet, so those screens are empty. The app creates no sample or demo records.

Anyone can register a normal account from the Register page. It has no admin access.

## Try the customer flow

1. As super admin, open **Enquiries** and click **Offer free trial** on an enquiry (or **Offer a free trial** for a new customer). This creates the organization, a *Head office* branch and the owner's login with a temporary password, and emails the owner.
2. Without an SMTP server, the email is saved as an `.eml` file in `EMS/App_Data/mail` (open it in Outlook, Thunderbird or a text editor). The confirmation page also shows the email and the temporary password once.
3. Sign in as the owner with that email and password. The setup wizard asks them to:
   1. choose their own password,
   2. complete the organization profile,
   3. say whether they run several shifts / 24x7 (hospital, hotel, BPO, call centre) and enter the shift timings, or one general shift,
   4. add employees (or skip).
4. They then land on their organization panel at `/Org`: **Dashboard**, **Employees**, **Attendance** (a month register; earlier months can be filled in or corrected), **Salary slips** (monthly salary pro-rated by paid days, printable), **Shifts** and **Organization profile**.

Salary slips do not apply statutory deductions (PF, ESI, professional tax, TDS) yet.

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
| `Email:SmtpHost`, `SmtpPort`, `UserName`, `Password`, `FromAddress` | `appsettings.json` / environment | Outgoing mail. With no `SmtpHost`, mail is saved to `Email:PickupDirectory` (default `App_Data/mail`) |

## Troubleshooting

- **`address already in use` on port 7134 or 5046**: another copy of the app is running. Stop it (Ctrl+C in its terminal), or find it with `netstat -ano | findstr :7134` and end that process.
- **`Failed to connect to 127.0.0.1:5432`**: the database is not running. Start Docker Desktop, then `docker compose up -d`.
- **`relation "..." does not exist`**: the tables are missing or out of date. Run `dotnet ef database update`.
