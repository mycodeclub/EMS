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
| ----------- | ---------------------- | ----------------- |
| Super admin | `superadmin@ems.local` | `SuperAdmin#2026` |


### Public demo logins

The app keeps one demo organization, **Greenfield Institute (Demo)**, with 12 employees on three shifts (General 09:00–17:30, Early 07:00–15:00, Night 22:00–06:00) and attendance from the 1st of the month three months back until yesterday. These logins are listed on the sign-in page, where each one signs in with one click:

| Role          | Email                      | Password    | Sees                                                                                     |
| ------------- | -------------------------- | ----------- | ---------------------------------------------------------------------------------------- |
| Admin (owner) | `admin@greenfield.test`    | `Demo@1234` | Every page of the organization panel                                                     |
| HR            | `hr@greenfield.test`       | `Demo@1234` | Dashboard, employees, attendance, punch in / out, shifts, import, own attendance & slips |
| Accounts      | `accounts@greenfield.test` | `Demo@1234` | Dashboard, salary slips, own attendance & slips                                          |
| Employee      | `employee@greenfield.test` | `Demo@1234` | Own attendance, shift, leave, profile, letters, resignation                              |

- The demo is rebuilt at midnight (`Demo:TimeZone`, default `Asia/Kolkata`), and on startup if it was last built before today. Only the demo organization and these four logins are deleted and recreated; real customers are never touched. The super admin can also click **Reset demo now** on _Customers & trials_.
- Demo logins cannot open the account pages (change password, email or delete account).
- Settings: `Demo:Enabled` (turn the demo off), `Demo:Password`, `Demo:TimeZone`.

### Onboarding a new employee

1. **Add employee** (HR or owner): personal details, job (biometric ID, reporting manager, employment type, shift, salary, probation end date), PAN / Aadhaar / bank account, and optionally **Give login access now**. Duplicate email, mobile, PAN or Aadhaar and impossible dates (under 14 at joining, probation ending before joining) are refused. Saving opens the employee's page.
2. **Joining checklist** on the employee's page and in their own **Me → Joining checklist**: personal details, PAN, Aadhaar, salary account, shift, and the documents to collect — PAN card, Aadhaar card, qualification certificate, cancelled cheque, passport-size photo, signed offer letter, plus the previous employer's relieving letter when they have prior experience. Files (PDF or image, up to 5 MB) are stored privately in `App_Data/documents`.
3. Documents the employee uploads wait for HR under **Leave & resignations → Joining documents to verify** (verify, or reject with a note). Documents HR uploads are verified at once.
4. The **Employees** list shows each person's onboarding progress; **Onboarding pending** lists only those with items left.
5. **Probation**: new employees start _On probation_ (6 months by default). **Confirm employment** makes them Active and adds a **confirmation letter** under Letters.

### Employee self-service

Staff with a login (HR, Accounts, Employee) get a **Me** menu:

- **My attendance**: each day with in / out times, their shift (timings, break, grace, full / half day), and the month's salary slip.
- **Leave**: balances (casual 12, sick 8, earned 15 a year by default, pro-rated for joiners), apply (half day allowed, up to 30 days back), cancel while pending. Sundays and holidays are not counted.
- **My profile**: photo (JPEG / PNG / WebP up to 2 MB, stored in `App_Data/photos`), personal details, address, emergency contact, previous jobs (adds up the prior experience), PAN and Aadhaar (shown masked), salary bank account.
- **Letters**: offer letter, an appraisal letter for each salary revision, and a relieving letter once a resignation is accepted. Print or save as PDF.
- **Resignation**: shows the notice period, submit with the last day asked for, withdraw until HR accepts. Once accepted the employee is _On notice_ and sees the countdown to the last working day.
- **Account & password** (everyone, owner too): change the sign-in email and password. Locked for the shared demo logins and while a super admin views a customer.

HR and the owner approve leave and accept or reject resignations under **Leave & resignations**. Approved leave marks the days _On leave_ in attendance and uses the balance. Accepting a resignation sets the last working day (the leaving date) and the _On notice_ status. Salary revisions are recorded on the employee's page and produce the appraisal letter; the notice period is set there too.

### Sign in as a customer

On **Customers & trials**, **Sign in as admin ↗** opens that organization's panel in a new tab, signed in as its owner. It uses a separate cookie that only `/Org` receives, so the admin console in the first tab stays signed in. A banner shows while you are viewing as the customer, changes are recorded under the super admin's id, and **End session** (or signing out of the admin console) ends it. The session lasts at most 2 hours. Only organizations that finished setup can be opened.

> These credentials are for local development only. The password lives in `appsettings.Development.json`. In any other environment set `SuperAdmin__Email` and `SuperAdmin__Password` (and `ConnectionStrings__DefaultConnection`) as environment variables. An existing account keeps its password; changing the setting later does not reset it.

After signing in, the super admin lands on the admin console at `/Admin`: a dashboard with charts, **Enquiries**, **Leads**, **Customers & trials** and **Billing**. Enquiries (from the website contact form) and free trials come from the database. Leads, paying customers and billing are not stored yet, so those screens are empty. Apart from the public demo organization below, the app creates no sample records.

Anyone can register a normal account from the Register page. It has no admin access.

## Try the customer flow

1. As super admin, open **Enquiries** and click **Offer free trial** on an enquiry (or **Offer a free trial** for a new customer). This creates the organization, a _Head office_ branch and the owner's login with a temporary password, and emails the owner.
2. Without an SMTP server, the email is saved as an `.eml` file in `EMS/App_Data/mail` (open it in Outlook, Thunderbird or a text editor). The confirmation page also shows the email and the temporary password once.
3. Sign in as the owner with that email and password. The setup wizard asks them to:
   1. choose their own password,
   2. complete the organization profile,
   3. say whether they run several shifts / 24x7 (hospital, hotel, BPO, call centre) and enter the shift timings, or one general shift,
   4. add employees (or skip).
4. They then land on their organization panel at `/Org`: **Dashboard**, **Employees**, **Attendance** (a month register; earlier months can be filled in or corrected), **Salary slips** (monthly salary pro-rated by paid days, printable), **Shifts** and **Organization profile**.

Salary slips do not apply statutory deductions (PF, ESI, professional tax, TDS) yet.

## Attendance: punch in / out and import

- **Month register** (`/Org/Attendance`): one status per employee per day. A small blue dot marks a day with punch times (hover to see in, out and hours worked); orange means late. Click a date to open that day.
- **Punch in / out** (`/Org/Attendance/Day`): enter in and out times per employee. Worked time, late arrival and the status (P / HD / A) are worked out from the employee's shift while you type, and can be overridden for leave or holidays.
  - Late: in time after shift start + grace minutes.
  - Present from the shift's full-day minutes, half day from its half-day minutes, otherwise absent. In with no out yet counts as present.
  - Night shifts: attendance belongs to the day the shift starts; an out time earlier than the in time is the next morning.
- **Import** (`/Org/Import`): employees and attendance from `.csv` or `.xlsx` (up to 5 MB / 20,000 rows). Templates download as Excel or CSV, blank or with sample rows; the Excel template has an Instructions sheet and drop-downs for status, gender and your shift codes. Every row is validated first and nothing is saved if any row has a problem.
  - Employees: new rows are added (blank codes numbered automatically); existing codes are skipped unless "Update existing employees" is ticked, in which case blank cells keep the current value.
  - Attendance: one row per employee per day, with an in time, a status, or both. Days already marked are skipped unless "Replace days already marked" is ticked. A _Biometric ID_ column can be used instead of _Employee code_ for device exports.
  - Dates are read day first (05-09-2026 is 5 September); `2026-09-05` and Excel date cells also work.

## Everyday commands

Run these from the `EMS` folder.

| Task                                        | Command                                                                            |
| ------------------------------------------- | ---------------------------------------------------------------------------------- |
| Add a migration after changing a model      | `dotnet ef migrations add <Name> -o Data/Migrations`                               |
| Apply migrations                            | `dotnet ef database update`                                                        |
| Check for model changes without a migration | `dotnet ef migrations has-pending-model-changes`                                   |
| Open a SQL prompt                           | `docker exec -it ems-postgres psql -U ems -d ems`                                  |
| Wipe the database and start over            | `docker compose down -v`, `docker compose up -d`, then `dotnet ef database update` |

Table and column names are case-sensitive in PostgreSQL, so quote them: `select * from "Enquiries";`

## Configuration

| Setting                                                             | Where                                               | Purpose                                                                                               |
| ------------------------------------------------------------------- | --------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:DefaultConnection`                               | `appsettings.json`                                  | PostgreSQL connection                                                                                 |
| `SuperAdmin:Email` / `SuperAdmin:Password`                          | `appsettings.json` / `appsettings.Development.json` | Account seeded at startup                                                                             |
| `Company`                                                           | `appsettings.json`                                  | Company details shown on the landing page                                                             |
| `Email:SmtpHost`, `SmtpPort`, `UserName`, `Password`, `FromAddress` | `appsettings.json` / environment                    | Outgoing mail. With no `SmtpHost`, mail is saved to `Email:PickupDirectory` (default `App_Data/mail`) |

## Troubleshooting

- **`address already in use` on port 7134 or 5046**: another copy of the app is running. Stop it (Ctrl+C in its terminal), or find it with `netstat -ano | findstr :7134` and end that process.
- **`Failed to connect to 127.0.0.1:5432`**: the database is not running. Start Docker Desktop, then `docker compose up -d`.
- **`relation "..." does not exist`**: the tables are missing or out of date. Run `dotnet ef database update`.

## Deploying to production

Production is the site4now (SmarterASP) IIS site in the FTP folder `ems/`, with the live PostgreSQL database on `pg8001.site4now.net`.

1. Work on `main` (or a feature branch merged into `main`).
2. Open a pull request **`main` → `release`** and merge it. The `release` branch accepts only pull requests.
3. `.github/workflows/deploy.yml` then:
   - builds, and checks that every model change has a migration;
   - publishes a self-contained `win-x64` build (no .NET install needed on the server, runs out of process under IIS);
   - writes `appsettings.Production.json` (connection string and super admin login) from GitHub secrets;
   - applies EF migrations to the live database (if one fails, the live site is not touched);
   - puts up `app_offline.htm`, uploads over FTPS, and takes it down again. The server's `App_Data` (photos, documents, sign-in keys, mail) is never overwritten.

Settings are in GitHub → **Settings → Environments → production**:

| Kind     | Name                           | Value                                                                    |
| -------- | ------------------------------ | ------------------------------------------------------------------------ |
| Secret   | `CONNECTION_STRING`            | Live database connection string                                          |
| Secret   | `FTP_USERNAME`, `FTP_PASSWORD` | site4now FTP login                                                       |
| Secret   | `SUPERADMIN_PASSWORD`          | Password of the super admin, used only when the account is first created |
| Variable | `FTP_SERVER`                   | `win8117.site4now.net`                                                   |
| Variable | `FTP_REMOTE_DIR`               | `ems/`                                                                   |
| Variable | `FTP_PROTOCOL`                 | `ftps`                                                                   |
| Variable | `SUPERADMIN_EMAIL`             | Super admin sign-in email (default `superadmin@ems.local`)               |
| Variable | `EMAIL_SMTP_HOST` | Outgoing mail server. Leave unset to save mail to `App_Data/mail` instead of sending |
| Variable | `EMAIL_SMTP_PORT`, `EMAIL_ENABLE_SSL` | `587` with STARTTLS or `465` with SSL; `true` (default) |
| Variable | `EMAIL_USERNAME`, `EMAIL_FROM_ADDRESS`, `EMAIL_FROM_NAME` | Mailbox login (e.g. `ems@bitprosofttech.com`), sender address and name |
| Secret | `EMAIL_PASSWORD` | Mailbox password |

On first start the app creates the roles, the super admin and the public demo organization, and rebuilds the demo every midnight (India time). Dates such as "today" follow `App:TimeZone` (default `Asia/Kolkata`), not the server's clock. Never put a production connection string or password in the repository.
