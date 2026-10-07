# My Diary V3 — Migration, Architecture & Operations Guide

**Audience:** Production deployment team, DBA team, functional/admin team
**Status:** Pre-production (replaces the existing **My Diary (Project B)** application)
**Last updated:** 2026-06-29

> This single document is the complete handover for taking **My Diary V3** to production.
> It covers what the system is, how it is built, how the database is provisioned, how to
> deploy it, and the day-2 SOPs (RBAC, Request Portal, Page Info) the functional team needs.
> Database scripts referenced here are bundled under `/db` (see [§12](#12-database-script-bundle)).

---

## Table of contents
1. [What V3 is (executive summary)](#1-what-v3-is)
2. [Solution architecture](#2-solution-architecture)
3. [Project / folder structure](#3-project--folder-structure)
4. [Module navigation & page alignment](#4-module-navigation--page-alignment)
5. [Page & route catalogue](#5-page--route-catalogue)
6. [Database architecture](#6-database-architecture)
7. [Authentication & session handling](#7-authentication--session-handling)
8. [SOP — RBAC (roles, modules, menus, permissions)](#8-sop--rbac)
9. [SOP — Request Portal](#9-sop--request-portal)
10. [SOP — Page Info & adding/aligning a new page](#10-sop--page-info--adding-a-new-page)
11. [Configuration reference](#11-configuration-reference)
12. [Database script bundle](#12-database-script-bundle)
13. [Build, publish & deploy](#13-build-publish--deploy)
14. [Production cutover plan (replacing Project B)](#14-production-cutover-plan)
15. [Security hardening](#15-security-hardening)
16. [Smoke-test / validation checklist](#16-validation-checklist)
17. [Known issues & technical debt](#17-known-issues--technical-debt)

---

## 1. What V3 is

My Diary V3 is a **consolidation of two existing .NET 8 Blazor applications** into one:

| Source | What it contributed |
|---|---|
| **Project B — My Diary** | The UI shell (Union Bank topbar + dark sidebar), 23 feature modules (dashboards, Focus 360, Business 360, MIS, Applications, Downloads, Departments, UBINET tools, etc.), form-login + session handling, dual-DB access (Oracle + SQL Server). |
| **Project A — Request Portal** | A custom **RBAC** engine, the **Request Portal** workflow (raise/route/handle requests with SLA), **Notifications/messaging**, masters & admin screens. |

The result keeps **Project B's look & navigation**, adopts **Project B's login/session**, and layers in **Project A's RBAC, Request Portal and Notifications**, all behind a single sign-on and a single sidebar with **module switching**.

- **224 routable pages**, **23 feature modules** (Project B) + the full Request Portal & Admin/RBAC page set (Project A).
- **.NET 8 Blazor Server** (interactive server render mode).
- **Two databases:** Oracle (core: requests, RBAC, masters, notifications, My Diary core) + SQL Server (gap-performance dashboards + login session table).

---

## 2. Solution architecture

### 2.1 Technology stack
| Concern | Technology |
|---|---|
| UI | Blazor **Server** (Interactive Server render mode, prerender **off**), **MudBlazor 8**, ApexCharts |
| Runtime | .NET 8 (LTS) |
| Data access | **Dapper** + `Oracle.ManagedDataAccess.Core` (Oracle) + `Microsoft.Data.SqlClient` (SQL Server) |
| Background jobs | Hangfire (notification dispatch) |
| Email | MailKit (SMTP) |
| Reporting/export | QuestPDF, ClosedXML / EPPlus, PDFsharp |
| Logging | Serilog (console + rolling file under `/Logs`) |
| Auth | Cookie scheme + custom `AuthenticationStateProvider` backed by encrypted browser storage |

### 2.2 Projects (clean, layered — **3 projects**)
```
My_Diary_V3.sln
└── src/
    ├── MyDiary.Core   → domain models, abstractions, business services
    │                     (also hosts the merged RequestPortal.Core.* namespaces)
    ├── MyDiary.Data   → Dapper repositories (Oracle + SQL Server)
    │                     (also hosts the merged RequestPortal.Data.* repos)
    └── MyDiary.Web    → the Blazor app (all UI, auth, jobs, hubs, features)
```

> **Why some types use the `RequestPortal.*` namespace:** Project A's business/data layers were
> merged **into** `MyDiary.Core` / `MyDiary.Data` (so there are only 3 projects), but their internal
> namespaces were preserved (`RequestPortal.Core.*`, `RequestPortal.Data.*`) to keep the migrated
> query logic byte-for-byte unchanged and avoid a risky mass-rename. This is internal only.

### 2.3 Request flow (high level)
```
Browser ──(HTTPS)──► Kestrel/IIS ──► Blazor Server circuit (SignalR)
                                      │
   AuthenticationStateProvider ◄──────┤  reads encrypted browser storage → ClaimsPrincipal
   (rp_role / md_role / rp_super_admin claims)
                                      │
                MudBlazor pages ──► Core services ──► Dapper repos ──► Oracle / SQL Server
                                      │
                Hangfire job ──► NotificationDispatchJob ──► MailKit / SignalR bell
```

---

## 3. Project / folder structure

`src/MyDiary.Web` (the app):
```
Components/
  App.razor              → root HTML document, render mode, global <head> (CSS/JS, viewport, theme-color)
  Routes.razor           → Router + AuthorizeRouteView + NavigationGuard
  Layout/
    MainLayout.razor     → topbar (logo, search, quick links, notification bell, user menu) + drawer
    NavMenu.razor        → module-switching sidebar (Main + module submenus)
    LoginLayout.razor    → minimal chrome for /login
  Pages/
    Login.razor          → form login (dev bypass available), Error.razor
Features/                → 23 Project B feature modules (Dashboards, Focus360, MIS, Applications, …)
RequestPortal/           → Project A, merged in (namespace RequestPortal.Web.*)
  Auth/                  → CurrentUserService, AuthorizationPolicies, PermissionRequirement, UserDirectory
  Services/              → ModuleContext
  Shared/                → PageHeader, PageInfoIcon, RequestTimeline
  Pages/                 → Requests/, Admin/, Admin/Rbac/, Masters/, Handler/, Oversight/, Inquiry/, Reports/, Profile/
  Features/PageInfo/     → IPageInfoService (page metadata)
Auth/                    → CustomAuthenticationStateProvider, AuthorizationPolicies, CurrentUser, NavigationGuard
Services/                → AppModuleNav (module switcher), AuthApiService, ReportStateService, encryption, …
Shared/                  → NotificationBell
Hubs/                    → NotificationHub, NotificationStream
Jobs/                    → NotificationDispatchJob (Hangfire)
Notify/                  → SmtpEmailSender (MailKit), NoOpSmsSender
wwwroot/css/             → app.css, requestportal.css, responsive-a11y.css, …
Program.cs               → DI wiring, middleware pipeline, Hangfire, auth
db/                      → all production DB scripts (see §12)
```

---

## 4. Module navigation & page alignment

The sidebar mirrors **Project A's module-switching** UX, driven by the scoped service
`MyDiary.Web.Services.AppModuleNav`:

- **After login** the sidebar shows the **Main** module (My Diary core links) followed by a
  **"MODULES"** heading listing the other modules.
- **Clicking a module** swaps the sidebar to that module's submenu (with a "← Back to Main"
  button) and updates the **active-module chip on the topbar**. The active module also
  auto-syncs from the URL (deep links / browser back stay in sync).

| Module | Sidebar items | Visible to |
|---|---|---|
| **Main** | Home, KPI, Focus 360°, Business 360°, Assurance Corner, Departments, Downloads, MIS | all |
| **Applications** | Applications grid, Ask Us, Suggestions, Qlik, Escalation Matrix | all |
| **Request Portal** | New Request, My Requests, Handler Inbox, My Dashboard, Request Dashboard, SLA Breach Report, Request Inquiry | all |
| **Notifications** | My Notifications, Broadcast, Notification Templates | all (admin items inside) |
| **Access Management** | Role Permissions, Modules, Menus, Role Allocation, Users, Roles, RBAC Audit, Admin Rights | App/Super Admin |
| **Admin Portal** | Employee/Branch/Units masters, SLA, Holidays, Routing, All Requests, Audit, Delegations, API Help | App/Super Admin |

**To change which module a route belongs to:** edit `AppModuleNav.Classify(rel)` (route → module
mapping) and the corresponding `case` block in `NavMenu.razor`. The default landing route per
module is `AppModuleNav.DefaultRoute(module)`.

---

## 5. Page & route catalogue

**Project B (feature modules)** — 23 modules under `Features/` (Landing dashboards, KPI/`dashboard_new_1`,
`focus360`, `business-new`, `Dashboards2` = Assurance Corner, `app-portal1` = Applications,
`departmentsnew`, `downloads2`, `MIS`, UBINET tools, etc.).

**Project A (Request Portal + Admin/RBAC)** — routes:
```
/requests/new  /requests/mine  /requests/{id}  /requests/team  /handler/inbox
/dashboards/handler  /oversight/dashboard  /inquiry/requests  /reports/sla  /notifications
/masters/users  /masters/roles  /masters/role-allocation  /masters/employees  /masters/branches  /masters/units
/admin/masters  /admin/sla  /admin/holidays  /admin/routing  /admin/templates  /admin/broadcast
/admin/delegations  /admin/page-config  /admin/requests  /admin/audit  /admin/setup-audit  /admin/api-help
/admin/rbac/modules  /admin/rbac/menus  /admin/rbac/role-permissions  /admin/rbac/admin-rights  /admin/rbac/audit
/profile
```

---

## 6. Database architecture

The app uses **two databases**. Connection-string names → engine:

| Conn-string name | Engine | Used by |
|---|---|---|
| `Oracle`, `MyDiaryDBConnection`, `VCardDBConnection`, `PortalDBConnection` | **Oracle** | Request Portal (`RP_*`), My Diary core (`MD_*`), all transactional + RBAC data |
| `SQLServerConnection`, `GapPerformanceConnection` | **SQL Server** | Gap-performance dashboards + the **login session table** (`session_dtls`) |

### 6.1 Oracle objects (schema owner: `RP_OWNER` in dev)
- **Base: 49 application tables** (**38 `RP_*`** + **11 `MD_*` / `MYDIARY_*`**), **3 sequences**,
  **129 indexes**, **1 view**, **0 stored procedures/functions** (Dapper-based — no PL/SQL).
- **Added by increments `RP_06`–`RP_12`:** `RP_M_EMPLOYEE`, `RP_ESCALATION_MATRIX` (+`RP_ESCALATION_SEQ`),
  `RP_M_REQTYPE_UNITTYPE`, `RP_M_UNIT_VERTICAL` (+`RP_MAP_SEQ`), `RP_ATTACHMENT_BLOB`; new columns
  `RP_REQUEST.EXT_ALERT_ID`/`EXT_SOURCE`, `RP_M_REQUEST_TYPE.ENTRY_MODE`, and `RP_NOTIFICATION.USER_ID`
  (replacing `EMP_CODE`).
- **Hangfire storage (auto-created at first run):** 11 `HF_*` tables (`HF_JOB`, `HF_JOB_STATE`,
  `HF_JOB_QUEUE`, `HF_SERVER`, `HF_SET`, `HF_HASH`, `HF_LIST`, `HF_COUNTER`, `HF_AGGREGATED_COUNTER`,
  `HF_DISTRIBUTED_LOCK`, `HF_JOB_PARAMETER`) — durable background jobs.

Key Oracle table groups:
| Group | Tables (examples) |
|---|---|
| Requests | `RP_REQUEST` (+`EXT_ALERT_ID`/`EXT_SOURCE`), `RP_REQUEST_ASSIGNEE`, `RP_REQUEST_ACTION`, `RP_REQUEST_CLARIFICATION`, `RP_REQUEST_FEEDBACK`, `RP_REQUEST_ATTACHMENT`, `RP_ATTACHMENT_BLOB`, `RP_SLA_PAUSE_LOG` |
| Masters | `RP_M_REQUEST_TYPE` (+`ENTRY_MODE`), `RP_M_UNIT_TYPE`, `RP_M_UNIT`, `RP_M_VERTICAL`, `RP_M_DEPARTMENT`, `RP_M_ACTIVITY`, `RP_M_SLA_CONFIG`, `RP_M_HOLIDAY_CAL`, `RP_M_HOLIDAY`, `RP_M_ROUTING_RULE`, `RP_M_ROUTING_ASSIGNEE`, `RP_M_EMPLOYEE` |
| Classification / Escalation | `RP_M_REQTYPE_UNITTYPE`, `RP_M_UNIT_VERTICAL`, `RP_ESCALATION_MATRIX` |
| RBAC | `RP_RBAC_MODULE`, `RP_RBAC_MENU`, `RP_RBAC_ROLE_PERM`, `RP_RBAC_USER_PERM`, `RP_RBAC_SUPER_ADMIN`, `RP_RBAC_APP_ADMIN`, `RP_RBAC_VERTICAL_LEVEL`, `RP_RBAC_VERTICAL_ADMIN`, `RP_RBAC_AUDIT` |
| Identity | `RP_USER`, `RP_ROLE`, `RP_USER_ROLE`, `RP_DELEGATION` |
| Notifications | `RP_NOTIFICATION` (keyed by `USER_ID`), `RP_NOTIF_OUTBOX`, `RP_M_NOTIF_TEMPLATE` |
| Audit / Page info | `RP_AUDIT_LOG`, `RP_PAGE_INFO` |
| Background jobs | `HF_*` (Hangfire.Oracle.Core; auto-created) |
| My Diary core | `MD_RBAC_MODULE`, `MD_RBAC_MENU`, `MD_ROLE`, `MD_ROLE_MODULE_PERM`, `MD_NOTIF_TEMPLATE`, `MD_INAPP_NOTIF`, `MD_NOTIF_OUTBOX`, `MD_DELEGATION`, `MYDIARY_LANDING_SITEMAP` |

### 6.2 SQL Server objects (database: `GapPerformanceDB`)
12 tables: `session_dtls` (login session) + gap-performance data (`Branches`, `Zones`,
`BranchPerformanceData`, `BranchProductMetrics`, `BranchStaffDetails`, `ComplianceMetrics`,
`LoanSanctions`, `PerformanceParameters`, `StaffGrades`, `StressAccounts`, `AppSettings`).

### 6.3 Connection-string encryption
Connection strings in `appsettings.json` are **AES-256 encrypted**; they are decrypted at startup
by `ConnectionStringDecryptor.DecryptAll(...)` (see `Program.cs`) and injected back as plain values
for the providers. The dev `appsettings.Development.json` uses **plaintext** strings against the
local docker DBs and **`127.0.0.1` (not `localhost`)** to force IPv4 (the docker listeners are
IPv4-only; `localhost`→IPv6 `::1` causes `ORA-12541`).

---

## 7. Authentication & session handling

- **Login** (`/login`) is Project B's form login. In production it validates credentials against
  the **AD validate API** (`ApiEndpoints:ValidateDomainUser`) and optional **mobile OTP**
  (`ApiEndpoints:VerifyMobileOtp`). A **dev bypass** (`DevSettings:BypassAuth=true`) logs in with the
  **entered Employee ID** as the identity — **must be `false` in production**.
- On success, `CustomAuthenticationStateProvider.MarkAuthenticatedAsync(staff)` builds a
  `ClaimsPrincipal` and stores it **AES-encrypted in browser session storage**. Claims include:
  `emplid`, name/email/phone, `privilege`, and RBAC claims **`md_role`**, **`rp_role`**,
  **`rp_super_admin`**, **`rp_app_admin`** (the last two resolved from `RP_RBAC_SUPER_ADMIN` /
  `RP_RBAC_APP_ADMIN`).
- A row is written to **`session_dtls`** (SQL Server) for concurrent-session control
  (upsert by username). Logout removes it.
- **Page protection:** because auth lives in browser storage (not the auth cookie), pages are
  **not** protected by endpoint `[Authorize]` (that would 302 before the circuit reads storage).
  Protection is enforced by **`NavigationGuard`** (client-side redirect to `/login`) + **sidebar
  link gating** via `<AuthorizeView Policy="…">`.
- **Render mode is `InteractiveServer` with prerender = false** — required because auth depends on
  JS-interop browser storage that is unavailable during prerender.

---

## 8. SOP — RBAC

### 8.1 Model
- **Roles** (`RP_ROLE` / enum `RoleCode`): BranchUser/Head, RoUser/Head, ZoUser/Head, CoUser/Head,
  CoVertAdmin, VertL1–L5, **AppAdmin**, **SuperAdmin**.
- **Modules** (`RP_RBAC_MODULE`) → **Menus** (`RP_RBAC_MENU`) → **Role permissions**
  (`RP_RBAC_ROLE_PERM`: CanView/Add/Modify/Delete/Authorize) → **per-user overrides**
  (`RP_RBAC_USER_PERM`, grant or explicit deny).
- **Admin tiers:** `RP_RBAC_SUPER_ADMIN` (max 2, seeded) → `RP_RBAC_APP_ADMIN` (granted by Super
  Admin). Super Admin implies App Admin.

### 8.2 Policies (in `AuthorizationPolicies`)
`AnyStaff` (= `Employee`), `AnyHandler`, `OpsOversight`, `VertAdmin`, **`AppAdmin`**, **`SuperAdmin`** —
all evaluated against `rp_role` / `rp_super_admin` / `rp_app_admin` claims.

### 8.3 How to log in as Super Admin (and verify RBAC)
1. Seeded Super Admin emp code is in `RP_RBAC_SUPER_ADMIN` (dev seed: **`860921`**). Manage via
   **Access Management → Admin Rights** (Super-Admin only).
2. Sign in as that emp id → the **Access Management** and **Admin Portal** modules appear in the
   sidebar; signing in as any other user hides them. That is the RBAC gate working.

### 8.4 Day-2 RBAC tasks (functional/admin team)
| Task | Where |
|---|---|
| Create modules / sub-modules | Access Management → **Modules** |
| Define menus under a module | Access Management → **Menus** |
| Grant per-role permissions | Access Management → **Role Permissions** |
| Allocate roles to users | Access Management → **Role Allocation** / **Users** / **Roles** |
| Grant App Admin | Access Management → **Admin Rights** (Super-Admin) |
| Review changes | Access Management → **RBAC Audit Log** |

> All these screens read/write the live `RP_RBAC_*` tables; changes persist immediately.

---

## 9. SOP — Request Portal

### 9.1 Concepts
- A **Request** is classified (Request Type → Unit Type → Unit → Vertical → Department → Activity),
  described, then submitted (`/requests/new`, a 3-step wizard: Classify → Describe → Review).
- **Routing** (`RP_M_ROUTING_RULE` / `RP_M_ROUTING_ASSIGNEE`) resolves the **L1 handler** by the
  most-specific matching rule.
- **Handlers** operate at vertical levels **L1–L5** (`/handler/inbox`). Actions, clarifications,
  reopens and feedback are recorded on `RP_REQUEST_ACTION` / `RP_REQUEST_CLARIFICATION` /
  `RP_REQUEST_FEEDBACK`.
- **SLA** (`RP_M_SLA_CONFIG` + holiday calendars `RP_M_HOLIDAY_CAL`/`RP_M_HOLIDAY`) computes a due
  time using working days; breaches surface in **SLA Breach Report** (`/reports/sla`). Auto-escalation
  advances unhandled requests up to L4.
- **Oversight** (L5/admin) monitors via **Request Dashboard** (`/oversight/dashboard`) and
  **Request Inquiry** (`/inquiry/requests`).

### 9.2 Admin setup order (do once per environment)
1. **Masters** (`/admin/masters`): Request Types, Unit Types, Units, Verticals, Departments, Activities.
2. **SLA** (`/admin/sla`) and **Holiday Calendars** (`/admin/holidays`).
3. **Routing rules** (`/admin/routing`) → assign L1 handlers.
4. **Notification templates** (`/admin/templates`) per event × channel.
5. **Delegations** (`/admin/delegations`) for cover during absence.

### 9.3 Notifications
- In-app **bell** (topbar) lists recent notifications + **"View all"** → `/notifications`.
- The **Hangfire** job `md.notif.dispatch` runs every minute: reads `RP_NOTIF_OUTBOX`, renders the
  matching `RP_M_NOTIF_TEMPLATE`, and sends via **email (MailKit)**, **SMS (stub)** or pushes the
  in-app bell. Configure SMTP under the `Smtp` section.

---

## 10. SOP — Page Info & adding a new page

### 10.1 Page Info (the ⓘ button)
- Every Request-Portal page can render a `<PageHeader PageKey="…">` with an **ⓘ icon**
  (`PageInfoIcon`) that shows help metadata for that page key.
- Metadata is served by `IPageInfoService`. For production, point it at the Oracle **`RP_PAGE_INFO`**
  table (seed scripts under `db/oracle/page_info/`); the dev build uses an in-memory provider.
- **To add/edit page help:** insert/update a `RP_PAGE_INFO` row keyed by the page's `PageKey`
  (title, description, steps), or edit via **Admin → Page Config** (`/admin/page-config`).

### 10.2 Adding a new page and aligning it to a module
1. Create the `.razor` page under the relevant folder with a unique `@page "/route"`.
2. If it's a Request-Portal/Admin page, put it under `RequestPortal/Pages/...` and keep the
   `@namespace RequestPortal.Web.Pages....` convention (the scoped `_Imports.razor` provides the
   `global::RequestPortal.*` usings).
3. Add a `<PageHeader PageKey="my-key" Title="…" Crumbs="…" />` for breadcrumbs + ⓘ help.
4. **Wire it to a module/sidebar:** add a `<MudNavLink Href="route">` in the right `case` of
   `NavMenu.razor`, and (if it should change the active module) add its route prefix to
   `AppModuleNav.Classify`.
5. **RBAC:** wrap admin-only links in `<AuthorizeView Policy="AppAdmin">`; register any new
   injected service in `Program.cs` (or rely on the convention scanner — class name ending in
   `Service`/`Services` implementing a `MyDiary.*`/`RequestPortal.*` interface is auto-registered).

---

## 11. Configuration reference

`appsettings.json` (production) — key sections:
```jsonc
"ConnectionStrings": {            // AES-256 encrypted in prod; decrypted at startup
  "Oracle": "…", "MyDiaryDBConnection": "…", "VCardDBConnection": "…", "PortalDBConnection": "…",
  "SQLServerConnection": "…", "GapPerformanceConnection": "…"
},
"ApiEndpoints": {
  "CryptoServiceUrl": "https://…",      // AES key/crypto service
  "ValidateDomainUser": "https://…/api/auth/validate",
  "VerifyMobileOtp":   "https://…/api/auth/verify-otp",
  "QlikUrl": "…", "SuggestionPortalUrl": "…", "AskUsPortalUrl": "…",
  "EscalationMatrixUrl": "…", "VCardBaseUrl": "…"
},
"Smtp": { "Host": "…", "Port": 25, "FromName": "My Diary", "FromAddress": "no-reply@bank",
          "Username": "…", "Password": "…", "UseStartTls": true },
"ApiKeys": {                         // server-to-server X-Api-Key secrets (UCCRMC + notifications)
  "UCCRMC": "…", "NotificationsPush": "…"   // inject via env: ApiKeys__UCCRMC / ApiKeys__NotificationsPush
},
"Focus360": { /* report title, bank name, logos */ },
"Serilog": { /* levels + file sink */ },
"DevSettings": { "BypassAuth": false }   // MUST be false in production
```
**API keys:** each `ApiKeys:{client}` entry is a shared secret; the matched client name becomes the
`api_client` claim. The auth handler ignores meta keys (`_`/`/` prefixed) and unset placeholders
(values starting with `<`), so an unconfigured key can never authenticate. Supply real values via
environment variables (`ApiKeys__UCCRMC`, `ApiKeys__NotificationsPush`) or a secret store — never commit them.
**Environment variables (recommended for secrets):** SMTP password, the AES key for
`ConnectionStringDecryptor` (e.g. `MD_CONN_ENCRYPTION_KEY`), and any API tokens — supply via the OS
/ container env or a secret store rather than committing them.

---

## 12. Database script bundle

All scripts live under `db/`:

| File | Purpose |
|---|---|
| `db/oracle/PROD_FULL_MIGRATION.sql` | **Single consolidated Oracle script** — structure (tables, sequences, indexes, FKs, view) **+** all seed (RBAC, masters, SLA, routing, templates, super-admin, MD core). Run this on the prod PDB. |
| `db/oracle/PROD_oracle_ddl.sql` | Structure only (live DDL export via `DBMS_METADATA`). |
| `db/oracle/RP_02_seed.sql`, `RP_04_superadmin.sql` | Request-Portal seed + bootstrap super-admin (also bundled into the consolidated file). |
| `db/oracle/01_schema.sql`, `02_seed.sql` | My-Diary-core `MD_*` structure + seed. |
| `db/oracle/page_info/` | `RP_PAGE_INFO` seed (page help text). |
| `db/sqlserver/PROD_sqlserver_ddl.sql` | `GapPerformanceDB` table DDL. |
| `db/sqlserver/01_session_schema.sql` | `session_dtls` login-session table. |
| **`db/oracle/00_RUN_ALL.sql`** | **Ordered runner** — applies the base migration then every incremental script (RP_06–RP_11) in order. Use this for a clean environment. |

**Incremental change scripts (idempotent — safe to re-run):**

| File | Purpose |
|---|---|
| `RP_06_escalation_matrix.sql` | `RP_M_EMPLOYEE` (employee view) + `RP_ESCALATION_MATRIX` (L1–L5 per activity) + seed. |
| `RP_07_classification_mapping.sql` | Interlink tables `RP_M_REQTYPE_UNITTYPE` + `RP_M_UNIT_VERTICAL` (RequestType→UnitType, Unit→Vertical) + seed. |
| `RP_08_uccrmc.sql` | UCCRMC alerts: `RP_REQUEST.EXT_ALERT_ID`/`EXT_SOURCE` + index; seeds the `UCCRMS Alert` request type and the fixed UCCRMC classification chain. |
| `RP_09_notification_userid_fix.sql` | Aligns `RP_NOTIFICATION` to the repo: `EMP_CODE` → `USER_ID`; adds `IX_RP_NOTIF_USER`. |
| `RP_10_entrymode_and_attachment_blob.sql` | `RP_M_REQUEST_TYPE.ENTRY_MODE` (MANUAL/API/BOTH) + check constraint; `RP_ATTACHMENT_BLOB` (attachment bytes in DB). |
| `RP_11_superadmins.sql` | **Production super admins `921592` & `713907`** (removes demo `860921`). |
| `RP_12_sample_users_routing_requests.sql` | Sample users (+emails/mobiles), routing rules making **713907 the L1 handler**, `VERT_L1` vertical assignment, and sample requests assigned to 713907. Dev/UAT convenience — review before prod. |

**Run order (Oracle):**
1. Create/identify the target schema owner (e.g. `RP_OWNER`) with a default tablespace + quota.
2. Connected **as that owner**, run `db/oracle/00_RUN_ALL.sql` (base + all increments), **or** run
   `PROD_FULL_MIGRATION.sql` then each `RP_06`…`RP_11` in numeric order.
   - If your schema name differs from `RP_OWNER`, either keep `RP_OWNER` as the owner, **or**
     find/replace `"RP_OWNER".` → `` (empty) in the base script and run as your owner.
   - The script sets `SET SQLBLANKLINES ON` for the MD seed (SQL\*Plus blank-line handling).
3. Verify counts (see [§16](#16-validation-checklist)).

**Run order (SQL Server):**
1. Create database `GapPerformanceDB` (or your prod name; update the conn-string).
2. Run `db/sqlserver/01_session_schema.sql` (creates `session_dtls`).
3. Run `db/sqlserver/PROD_sqlserver_ddl.sql` for the gap tables, **or** restore a **BACPAC**
   (recommended — preserves indexes + data) produced with `sqlpackage` from the source DB.

> **No stored procedures/functions** exist in the Oracle app schema — all business logic is in C#
> (Dapper), so there is nothing PL/SQL to port. (Some gap dashboards in SQL Server reference SPs such
> as `usp_GetBranchProfile` — create those in `GapPerformanceDB` if those specific widgets are needed.)

---

## 13. Build, publish & deploy

### 13.1 Prerequisites on the host
- **.NET 8 Hosting Bundle** (ASP.NET Core Runtime 8.x) — for IIS; or the .NET 8 runtime for Kestrel.
- Oracle client is **not** required (`Oracle.ManagedDataAccess.Core` is managed).
- Network access from the app host to: Oracle listener, SQL Server, SMTP, and the AD/crypto APIs.
- HTTPS certificate.

### 13.2 Build & publish
```bash
cd My_Diary_V3
dotnet restore
dotnet publish src/MyDiary.Web/MyDiary.Web.csproj -c Release -o ./publish
```
Deploy the `./publish` folder.

### 13.3 Hosting options
- **IIS** (typical for bank/Windows): install the .NET 8 Hosting Bundle, create a site/app pool
  (No Managed Code), point it at `./publish`. `web.config` is generated by publish.
- **Kestrel / Windows Service / container**: run `dotnet MyDiary.Web.dll` behind a reverse proxy
  (IIS/Nginx) terminating TLS. Set `ASPNETCORE_ENVIRONMENT=Production`.

### 13.4 Required production settings
- `DevSettings:BypassAuth = false`.
- Real, **encrypted** connection strings + the decryption key via env var.
- SMTP credentials; `UseStartTls=true` if your relay requires it.
- Hangfire uses **durable Oracle storage** (`Hangfire.Oracle.Core`, `HF_*` tables auto-created under
  the Oracle schema on first run) — queued jobs survive restarts. No action needed.
- `app.UseHttpsRedirection()` + HSTS are already enabled.

---

## 14. Production cutover plan (replacing Project B)

1. **Provision DBs** (§12) in production; confirm connectivity from the app host.
2. **Publish** V3 to a **parallel** site/URL (do not overwrite Project B yet).
3. **Configure** `appsettings.json` (encrypted conn-strings, API endpoints, SMTP, BypassAuth=false).
4. **Smoke test** on the parallel URL using the [checklist](#16-validation-checklist) with a pilot
   group (a Super Admin + a few normal users).
5. **Seed RBAC/masters** for go-live (modules, role permissions, request masters, SLA, routing).
6. **Cutover:** repoint the production hostname/DNS (or IIS binding) from Project B to V3 during a
   maintenance window. Keep Project B deployed but **stopped** for rollback.
7. **Post-cutover:** monitor Serilog (`/Logs`), Hangfire dashboard (`/hangfire`, dev only — gate in
   prod), and the SLA/notification jobs for the first few cycles.
8. **Rollback plan:** if blocking issues, repoint DNS/binding back to Project B (its DB is
   independent of the new Oracle schema, so no data rollback is needed).

---

## 15. Security hardening (already applied / to confirm)
- HTTPS redirect + **HSTS**; cookies `HttpOnly` + `Secure` + `SameSite=Strict`.
- **Parameterised SQL only** (Dapper) — the SQL-injection-prone legacy `UserSessionService` was
  replaced by the parameterised `SessionRepository`.
- Connection strings **AES-256 encrypted** at rest; secrets via env/secret store.
- Anti-forgery enabled; centralised error boundary + circuit error handling.
- **Confirm before go-live:** `BypassAuth=false`, Hangfire dashboard not publicly exposed,
  Serilog not logging sensitive payloads, and least-privilege DB accounts (the app account needs
  DML on its own schema only).

---

## 16. Validation checklist
**Build/run**
- [ ] `dotnet build` → 0 errors / 0 warnings.
- [ ] App starts; `/login` returns 200; `/_blazor/negotiate` reachable.

**DB**
- [ ] Oracle base + increments applied (run `db/oracle/00_RUN_ALL.sql`): `user_tables` ≈ **70+**
  (49 base + `RP_06`–`RP_12` objects + 11 `HF_*` after first app start).
- [ ] `RP_RBAC_SUPER_ADMIN` = **921592, 713907** only (demo `860921` removed).
- [ ] New objects exist: `RP_ATTACHMENT_BLOB`, `RP_ESCALATION_MATRIX`, `RP_M_REQTYPE_UNITTYPE`,
  `RP_M_UNIT_VERTICAL`; columns `RP_REQUEST.EXT_ALERT_ID`, `RP_M_REQUEST_TYPE.ENTRY_MODE`,
  `RP_NOTIFICATION.USER_ID`.
- [ ] Seed counts present: `RP_ROLE`, `RP_RBAC_MODULE`, `RP_RBAC_ROLE_PERM`, masters, `RP_M_NOTIF_TEMPLATE`.
- [ ] SQL Server: `session_dtls` exists; gap tables present.

**Auth & RBAC**
- [ ] Login works (AD API in prod); session row written to `session_dtls`.
- [ ] Super Admin sees **Access Management + Admin Portal**; a normal user does not.
- [ ] Module switching updates sidebar + topbar chip; deep links keep module in sync.

**Functional**
- [ ] Raise a request end-to-end (Classify→Describe→Review→Submit); it routes to an L1 handler and
  appears in **My Requests**; opening its **detail** page works (with attachment upload → DB BLOB).
- [ ] SLA due date computed; breach shows in SLA report.
- [ ] On submit, **both requester and assigned handler** receive a **bell + email** (Hangfire drains
  `RP_NOTIF_OUTBOX`; verify via SMTP/inbox).
- [ ] **Broadcast** (`/admin/broadcast`) audience preview lists active users; send delivers.
- [ ] **UCCRMC APIs** (`X-Api-Key`): create alert / status / dashboard return 2xx; `UCCRMS Alert` is
  hidden from the manual New-Request form (API-only).
- [ ] Dashboards (Assurance Corner, Focus 360, MIS) render data from the gap/Oracle DBs.

---

## 17. Known issues & technical debt
- **Demo-backed Masters pages:** `Roles`, `Users`, `Branches`, `Employees`, `Role-Allocation` were
  demo-data-backed in Project A (read from an in-memory `DemoStore`). They render **empty grids**
  until wired to live queries / views. The Admin pages that use the Dapper repos (SLA, Holidays,
  Routing, Templates, etc.) use real `RP_*` data.
- **SQL Server gap dashboards** reference a few stored procs (e.g. `usp_GetBranchProfile`,
  `usp_GetBranchStaffSummary`) and a `zone_master`/VCard object not present in the sample DB — create
  these in `GapPerformanceDB`/Oracle if those specific widgets are required.
- **Hangfire** now uses **durable Oracle storage** (`Hangfire.Oracle.Core`; `HF_*` tables auto-created
  under the app schema). Jobs and the `md.notif.dispatch` recurring job persist across restarts.
- **Namespaces:** Project A's merged code retains `RequestPortal.*` namespaces (internal only; no
  functional impact). A future cleanup could unify them to `MyDiary.*`.
- **Build warnings** from migrated A/B code are suppressed via `<NoWarn>` (documented in each
  `.csproj`); they are inherited nullable/analyzer warnings, not new issues.

---

## 18. Post-migration enhancements (change log)

Changes made after the initial v3.0 migration. All are in `main`; DB deltas are the idempotent
`RP_06`–`RP_11` scripts (see [§12](#12-database-script-bundle)).

### 18.1 Escalation Matrix
- **Pages:** `/requests/escalation-matrix` (all staff — view L1–L5 contacts for an activity) and
  `/admin/escalation-matrix` (RP Admin — upload/download + view/edit PF-based mapping; IP number &
  Generic Mail entered per employee, other fields pulled from the employee view).
- **DB:** `RP_M_EMPLOYEE`, `RP_ESCALATION_MATRIX` (`RP_06`). Excel upload/download templates carry
  **names, not IDs**.

### 18.2 Interlinked classification (stored in DB)
- Full cascade **Request Type → Unit Type → Unit → Vertical → Department → Activity**, driven by
  mapping tables `RP_M_REQTYPE_UNITTYPE` and `RP_M_UNIT_VERTICAL` (`RP_07`).
- Managed at **Admin Portal → Classification Mapping** (`/admin/classification-map`).

### 18.3 UCCRMC command-centre integration (REST APIs)
- **Module:** new **API Admin** sidebar module (App-Admin gated) with two help pages:
  **Notifications API** (`/admin/api-help`) and **UCCRMC Alerts API** (`/admin/api-help/uccrmc`).
- **Auth:** `X-Api-Key` header (see [§11](#11-configuration-reference)); identifiers are employee/PF codes.
- **Endpoints** (`UccrmcController`):
  - `POST /api/uccrmc/alerts` — raise a **UCCRMS Alert** ticket. Stores the **Alert ID**, sets the
    passed **assignee** (bypasses routing), records the **requester**. Idempotent on Alert ID.
  - `GET /api/uccrmc/alerts/{alertId}/status` — ticket status / level / assignee / SLA / overdue.
  - `GET /api/uccrmc/alerts/dashboard?from=&to=` — closure & pendency aggregates.
- **Notifications API** (`NotificationsController`, restored): `POST /api/notifications` (push) and
  `GET/POST /api/notifications/user/{userId}/…` (list / unread-count / read / read-all).
- **DB:** `RP_REQUEST.EXT_ALERT_ID` + `EXT_SOURCE` and the seeded `UCCRMS Alert` type/chain (`RP_08`);
  `RP_NOTIFICATION` column fix (`RP_09`). Alerts flow into the **Request Dashboard**; the Oversight
  page shows a dedicated UCCRMC closure/pendency card.

### 18.4 Request-type entry mode (manual vs API)
- `RP_M_REQUEST_TYPE.ENTRY_MODE` = `MANUAL` | `API` | `BOTH` (`RP_10`). **UCCRMS Alert = API** — hidden
  from the New Request form; raisable only via the API.
- Configured at **Request Portal Masters** (`/admin/masters`) → **Request types → Availability**.

### 18.5 Attachments stored in the database
- Request attachments now persist as a **BLOB** in `RP_ATTACHMENT_BLOB` (`RP_10`) via `DbAttachmentStore`
  (replaces the filesystem store). The New Request and Request Detail upload controls were fixed for
  MudBlazor 8 (`ActivatorContent`).

### 18.6 Masters module (Super Admin)
- A dedicated **Masters** module gathers all `/masters/*` pages + `/admin/masters` under Super-Admin
  gating (page-level `SuperAdminOnly` guard, not just nav-hiding).

### 18.7 Super admins
- Production super admins are **`921592`** and **`713907`** (`RP_11`); the demo `860921` is removed.
  App cap remains **2** (`RbacConstants.MaxSuperAdmins`).

### 18.8 Current-user resolution fix (production correctness)
- Session-based auth stores the principal in encrypted **session storage**, not a cookie. The
  `CurrentUserService` was reading the anonymous HttpContext principal on the circuit, leaving
  `EmpCode`/`Roles` empty (caused `ORA-01400` on submit and empty "My Requests"). Fixed to fall back
  to the `AuthenticationStateProvider`, and **MainLayout** now warms the resolution once per circuit
  in an async context so the synchronous `ICurrentUser` getters are safe (no sync-over-async session
  read on the click path). `HandlerDashboard` role math was guarded against empty roles.

### 18.9 Notifications & broadcast (delivery verified)
- **Broadcast audience** now resolves from live data (`SqlBroadcastDirectory` over `RP_USER`) instead
  of the empty placeholder — Bank-wide returns all active users; Vertical scope filters by
  `RP_RBAC_VERTICAL_LEVEL`.
- **Request notifications on submit** deliver to **both the requester and the assigned handler**
  (bell + email + SMS-outbox). Fixed several defects: (a) `OracleHelper.Cmd` now binds parameters by
  name (`BindByName=true`) — a placeholder used multiple times no longer throws `ORA-01008`;
  (b) `NotificationService.EnqueueAsync` and `DispatchPendingAsync` now run in their own committed
  transactions — outbox/bell writes were previously issued after the request commit with no
  transaction and silently rolled back; (c) the Hangfire dispatch job now also drains the Request
  Portal outbox (`RP_NOTIF_OUTBOX`) via `INotificationService.DispatchPendingAsync`, not just the
  My-Diary bell repo. Verified end-to-end against a MailHog SMTP sink (email captured for handler +
  requester). Configure real SMTP via the `Smtp` section for prod.

### 18.10 Assurance Snapshot (branch assurance dashboard)
- New **Assurance Snapshot** page (`/assurance-snapshot`), placed **immediately after Focus 360** in
  the Main-module sidebar (expanded + mini), visible to **all employees** (ungated).
- **Same architecture/data as Focus 360:** the page consumes **`IGapService.BuildReportAsync`**
  (the merged, final Focus 360 service) over the **same SQL Server gap tables**
  (`BUSINESS_360_DATA` × `BUSINESS_360_PARAMETER_MASTER`, `F360_CATEGORY_COLOR`, branch lookups).
  It is **fully parameter-driven** — rows are grouped Category → SubCategory and rendered generically
  using the report's own category colours/section labels. No hardcoded values.
- **Parameterised config** (`GapReportSettings`, `Focus360` section):
  `AssuranceReportName` (title) and `AssuranceCategories` (list) — the categories that make up the
  snapshot; **empty = render every category** returned for the branch.
- The final **Focus 360** code was merged from the delivered `Focus360.zip` as-is (namespace adapted
  `My_Diary_V2` → `MyDiary.Web`); `focus360.css`/`focus360.js` refreshed.
- **RBAC:** `RP_13_assurance_menu.sql` registers module `DASH.ASSURANCE` + menu `/assurance-snapshot`
  and grants `CAN_VIEW` to **all 16 roles**.
- **Dev data:** `db/sqlserver/GAP_02_assurance_seed.sql` seeds the assurance content into the same
  gap tables — `BUSINESS_360_PARAMETER_MASTER`, `F360_CATEGORY_COLOR`, `BUSINESS_360_DATA_MyDiary`
  (8 categories, 59 parameters for branch **732 DAVANGERE-MANDIPET**) plus minimal branch/staff
  scaffolding and empty `View_TPPD_*` views so `BuildReportAsync` runs. `appsettings.Development.json`
  sets `Focus360:AssuranceCategories` to the 8 assurance categories.

### 18.11 Startup / hosting fixes
- Hangfire recurring job registered via `IRecurringJobManager` (DI) instead of the static
  `RecurringJob` API (which needs `JobStorage.Current`).
- **Hangfire now uses durable Oracle storage** (`Hangfire.Oracle.Core`) instead of in-memory —
  `HF_*` tables are auto-created under the Oracle schema on first run; queued/recurring jobs survive
  restarts. Uses the `ConnectionStrings:Oracle` string. New offline packages bundled in `nuget/`.
- Production `appsettings.json` now includes the `Oracle` connection string and the `ApiKeys` section.

> **Remaining for go-live (unchanged from §13.4/§17):** supply real encrypted connection strings +
> API-key secrets via env/secret store; keep `DevSettings:BypassAuth=false`; wire the remaining
> demo-backed Masters grids (Roles/Users/Branches/Employees) to live queries; gate `/hangfire` in prod.

---

*End of guide. DB scripts: `db/oracle/00_RUN_ALL.sql` (base + `RP_06`–`RP_11`), `db/sqlserver/PROD_sqlserver_ddl.sql`.*
