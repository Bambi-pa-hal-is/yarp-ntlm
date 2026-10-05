# Migrating a Windows-authenticated legacy app with YARP

This sample shows how to use YARP to move a legacy application to a new stack one feature at a time (the "strangler fig" pattern). The legacy app keeps using Windows authentication, and the new app uses JWTs.

**The scenario:** the fictional *Ekdala Municipality* has an old, Windows-authenticated case management system that all departments use. Building permits are the first module rewritten as a modern React app. Employees use one URL, and the gateway decides which system serves each page. All data is hardcoded and made up.

```
                         ┌───────────────────────────────────┐
 Browser ──► Gateway ────┤ /app/**                   ──────► │ ModernApp  Building permits (React + JWT)       :5102
            (YARP) :5100 │ /api/permits/**           ──────► │
                         │ /api/reports/permits      ──────► │
                         │ everything else           ──────► │ LegacyApp  Case management (Razor, Windows auth) :5101
                         └───────────────────────────────────┘
```

Both apps share one URL space. An API keeps its URL when it's rebuilt in the modern app; only the gateway's route for it changes (see [Migrating an endpoint](#migrating-an-endpoint)).

| Project | Port | Authentication | What it is |
|---|---|---|---|
| `Gateway` | 5100 | none (anonymous) | YARP. Routes by URL and passes NTLM/Kerberos through with `Bambi.Yarp.Ntlm` |
| `LegacyApp` | 5101 | Windows (NTLM/Kerberos) | *Case Management System 3.2*: the legacy code base. Also issues JWTs at `/api/users/me` |
| `ModernApp` | 5102 | JWT bearer | *Building Permits*: the new code base. React app at `/app`, plus the APIs migrated so far |

## What's in each app

**LegacyApp:** server-rendered Razor Pages with a deliberately dated look.

| URL | Page |
|---|---|
| `/` | Start page: open cases per department and the latest cases |
| `/cases` | Case list with department, status and text filters |
| `/cases/{number}` | Case details and case log. Migrated building-permit cases link to the new system |
| `/residents` | Resident register (requires the `CaseOfficer` role) |
| `/api/cases` | Legacy JSON API, not migrated yet |
| `/api/users/me` | Issues the JWT cookie for the new app |

**ModernApp:** a React (Vite + React Router) single-page app.

| URL | Page | API |
|---|---|---|
| `/app/` | Overview: counts, unassigned applications, your open cases | `GET /api/permits` |
| `/app/permits` | Applications, filtered by status | `GET /api/permits?status=` |
| `/app/permits/{id}` | Application details, timeline and decision form (`PermitOfficer`) | `GET /api/permits/{id}`, `POST /api/permits/{id}/decision` |
| `/app/reports` | Statistics (`Admin`) | `GET /api/reports/permits` |

Decisions are stored in memory and reset when ModernApp restarts. The apps link to each other. Navigating between them is a full page load through the gateway, and the user stays signed in to both.

## Sign-in flow

1. The browser opens `http://localhost:5100/app/`. The gateway serves the React app from ModernApp.
2. React calls `GET /api/users/me`. The gateway routes it to LegacyApp, which asks for Windows authentication. The browser signs in automatically with NTLM/Kerberos. `app.SupportNtlm()` in the gateway keeps that handshake on one dedicated backend connection.
3. LegacyApp maps the user's Windows groups to application roles (`Authorization:RoleMappings`), creates a JWT with the user's name, department and roles, and returns it as the HttpOnly cookie `access_token`.
4. React calls a migrated API, such as `/api/permits`. The browser sends the cookie, and because the `modern` cluster has `"JwtCookie"` metadata, the gateway moves it into `Authorization: Bearer ...` (see `Gateway/Program.cs`).
5. ModernApp validates the JWT (issuer, audience, signing key, lifetime) and authorizes by role.
6. When the token expires, ModernApp returns 401, and React calls `/api/users/me` again, which issues a fresh token without any prompt.

## Running

### Visual Studio + IIS Express

1. Open `yarp-ntlm.slnx` in the repository root, or the smaller `ReverseProxy.Ntlm.Sample.slnx` in this folder.
2. Select the launch profile **Gateway + Legacy + Modern (IIS Express)** (from `ReverseProxy.Ntlm.Sample.slnLaunch`; only available when opening the sample's own solution), or set the three projects as multiple startup projects with the **IIS Express** profile.
3. Press F5 and open `http://localhost:5100/` (case management) or `http://localhost:5100/app/` (building permits).

IIS Express settings come from each project's `Properties/launchSettings.json`:

- **LegacyApp:** Windows authentication on, anonymous off.
- **Gateway:** anonymous only. The gateway must not authenticate the user itself; it only forwards the handshake.
- **ModernApp:** anonymous only. It authenticates JWTs in code.

### Without IIS Express

Run each project with `dotnet run` (the non-IIS launch profiles). On Kestrel, LegacyApp does Windows authentication with the Negotiate handler.

## Building the React app

`ModernApp.csproj` runs `npm install` and `npm run build` during the build and outputs the app to `ModernApp/wwwroot/app`. Without Node.js the step is skipped with a message, and `/app` won't work until you install Node.js and rebuild. For hot reload, run `npm run dev` in `ModernApp/ClientApp` while the gateway is running; Vite proxies `/api` to the gateway.

## Roles

Roles are mapped from Windows groups in `LegacyApp/appsettings.json` (`Authorization:RoleMappings`). The legacy app uses them for its own pages, and they're copied into the JWT for the new app.

| Role | Grants |
|---|---|
| `Employee` | Access to the building permit app |
| `CaseOfficer` | The resident register in the legacy app |
| `PermitOfficer` | Making decisions on building permits |
| `Admin` | Reports (and everything a case officer can do in the legacy app) |

`Group` is either a group name (`EKDALA\SG-Building-Permits`) or a SID (`S-1-5-32-545`). Use SIDs for built-in groups, because their names depend on the Windows display language (on Swedish Windows, `BUILTIN\Users` is `BUILTIN\Användare`).

For the local demo, every Windows user (`BUILTIN\Users`) gets `Employee`, `CaseOfficer` and `PermitOfficer`. `Admin` comes from `BUILTIN\Administrators`, which only applies when UAC doesn't filter the token. For a normal local admin account, Administrators is a deny-only group, so **Reports** isn't shown and its API returns 403. Map one of your own groups to `Admin` to try it.

## Migrating an endpoint

Endpoints move one at a time and keep their URLs. For example, to rebuild the legacy `GET /api/cases`:

1. Implement `GET /api/cases` in ModernApp, returning the same response shape and protected by JWT and roles. Add it to the `/api` group in `ModernApp/Program.cs`.
2. Add a route for it to the `modern` cluster in `Gateway/appsettings.json`. A commented-out `cases-api` route is already there:

   ```json
   "cases-api": {
     "ClusterId": "modern",
     "Match": { "Path": "/api/cases/{**catch-all}", "Methods": [ "GET" ] }
   }
   ```

   `Methods` lets you move only the reads first. Leave it out to move every method. The route doesn't need any JWT settings: every request to the `modern` cluster gets the cookie moved into the `Authorization` header.
3. Callers don't change, because the URL is the same. The gateway reloads `appsettings.json` on change, so the switch (and a rollback) needs no redeploy of the gateway.
4. Once nothing reaches the legacy implementation any more, delete it from LegacyApp.

Pages move the same way: give the new page a route to the `modern` cluster, and point the legacy menu at it.

## Before using this for real

- The JWT signing key is in `appsettings.json` for convenience. Move it to a secret store and share it between LegacyApp and ModernApp (or switch to asymmetric keys, so ModernApp only has the public key).
- The sample uses HTTP so that IIS Express serves HTTP/1.1. Behind HTTPS, the cookie is marked `Secure` automatically via `X-Forwarded-Proto`. Browsers that connect over HTTP/2 are told to retry over HTTP/1.1 when Windows authentication is required (see `Bambi.Yarp.Ntlm`).
- The cookie is `SameSite=Strict`, which together with the JSON-only API protects the POST endpoints from cross-site requests. Keep it that way, or add antiforgery tokens.
- Lock the legacy and modern apps down so they're only reachable through the gateway.
