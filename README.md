# imockey

[![.NET](https://github.com/phduarte/imockey/actions/workflows/dotnet.yml/badge.svg)](https://github.com/phduarte/imockey/actions/workflows/dotnet.yml)
[![Container](https://github.com/phduarte/imockey/actions/workflows/container.yml/badge.svg)](https://github.com/phduarte/imockey/actions/workflows/container.yml)

A multi-user HTTP server for creating mocked endpoints. Useful for integration tests, stress tests, chaos simulation, debugging, and decoupled development.

> Also available in [Português (pt-BR)](README.pt-BR.md).

## Table of Contents

- [Concepts](#concepts)
- [Quick Start](#quick-start)
- [Accounts and Sign-in](#accounts-and-sign-in)
- [Applications](#applications)
- [Route Manager](#route-manager)
- [Calling a Route](#calling-a-route)
- [Pattern Matching Rules](#pattern-matching-rules)
- [Response Body Variables](#response-body-variables)
- [Use Cases](#use-cases)
- [Reserved Routes](#reserved-routes)
- [Persistence](#persistence)
- [Deployment](#deployment)
- [Development](#development)
- [Market Analysis](#market-analysis)
- [Technologies](#technologies)

---

## Concepts

**User** — Someone who signs in to imockey. Each user only sees and manages their own applications.

**Application** — A group of routes that simulates one real API. A user can have any number of applications. Each one has a **slug** that, prefixed with the owner's username (`{username}-{slug}`, e.g. `alice-store`), becomes the first segment of its routes' URLs.

**Route** — A non-functional copy of a real endpoint. Returns configured responses without any business logic. Every route belongs to one application.

**Pattern** — The address of the mocked endpoint inside its application (without host, username or slug). It may contain wildcards and validation types.

**Processing time** — An artificial delay applied before responding, useful for simulating latency or timeout.

Routes are managed only through the web UI; there is no management REST API.

---

## Quick Start

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# clone and run
git clone https://github.com/phduarte/imockey
cd imockey
run.bat                          # Windows
dotnet run --project Imockey    # any platform
```

`run.bat` frees ports 5087 and 7253 if they are in use, starts the server and opens the browser at `http://localhost:5087`.

1. Create an account at `/account/register`, for example with username `alice` (or sign in with GitHub, if configured).
2. Create an application, for example **Store API** with slug `store`. Its routes will live under `/alice-store`.
3. In the application's Route Manager, create a route, for example `GET /v1/products`.
4. Call it: `curl http://localhost:5087/alice-store/v1/products`.

---

## Accounts and Sign-in

| Address | Description |
| --- | --- |
| `/account/login` | Sign in with username and password, or with GitHub |
| `/account/register` | Create an account with username, optional display name and password |
| `/account/logout` | Signs out (the **Sair** button in the top bar) |

Every UI page requires a signed-in user; anonymous visitors are redirected to the login page. Route calls do **not** require sign-in.

### Username and password

- Username: 3 to 32 characters — letters, numbers and `-` (no `-` at the start, at the end or doubled). It is part of the route URLs, so characters that are not URL-safe are rejected. It is stored lowercased and must be unique.
- Display name: optional; shown in the top bar. Defaults to the username.
- Password: at least 8 characters, stored as a salted hash (ASP.NET Core Identity `PasswordHasher`).
- The session cookie lasts 14 days (sliding). If the user no longer exists in the database, the session is rejected and the user must sign in again.

### GitHub

The **Sign in with GitHub** button only appears when an OAuth App is configured:

1. Create an OAuth App at <https://github.com/settings/developers> with callback URL `http://localhost:5087/signin-github` (adjust host/port).
2. Store the credentials outside the repository, e.g. with user-secrets:

   ```bash
   dotnet user-secrets --project Imockey init
   dotnet user-secrets --project Imockey set "Authentication:GitHub:ClientId" "<client id>"
   dotnet user-secrets --project Imockey set "Authentication:GitHub:ClientSecret" "<client secret>"
   ```

   Or use the environment variables `Authentication__GitHub__ClientId` and `Authentication__GitHub__ClientSecret`.

The first GitHub sign-in creates an imockey user linked to that GitHub account, with a username derived from the GitHub login (lowercased, non-URL-safe characters replaced by `-`; a numeric suffix such as `-2` is added if it is already taken). GitHub users and username/password users are separate accounts — they are never linked.

---

## Applications

After signing in, the home page (`/`) lists your applications and has a form to create a new one.

| Field | Required | Description |
| --- | --- | --- |
| Name | yes | Display name |
| Slug | no | Lowercase letters, numbers and `-`, up to 50 characters. Generated from the name when empty (`Payment Service` → `payment-service`). The form previews the resulting base URL. |
| Description | no | Free text |

The stored slug is always prefixed with your username: slug `payment-service` for user `alice` becomes `alice-payment-service`, the first segment of the routes' URLs. It must be unique across all users and cannot be a [reserved route](#reserved-routes).

After creating an application you are taken to its Route Manager. The list shows each application's base URL, number of routes and creation date. Click the name (or **→**) to open its Route Manager. **✕** deletes the application **and all of its routes** (asks for confirmation first).

---

## Route Manager

Opened at `/apps/{applicationId}`. Only the owner can open it; anyone else sees "Aplicação não encontrada" (application not found), whether it exists or not.

The header shows the application's base URL (`http://localhost:5087/{username}-{slug}/...`). The screen is split into two columns:

- **Left** — form for creating or editing routes
- **Right** — list of the application's routes

The list updates automatically in real time via Blazor Server (SignalR) when routes of the application change.

### Form

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| Description | text | — | Free text to document the purpose of the route. Optional. |
| HTTP Method | dropdown | GET | GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS |
| Pattern (URL) | text | — | Endpoint address inside the application. Required; a leading `/` is added if missing. Supports wildcards (see [Pattern Matching Rules](#pattern-matching-rules)) |
| Status Code | number | 200 | HTTP response code, between 100 and 599. Suggestions for common codes. |
| Processing time (ms) | number | 0 | Delay in milliseconds before responding (negative values become 0) |
| Content-Type | dropdown | application/json | Response MIME type (JSON, XML, text, HTML, CSV, images, etc.) |
| Response body | textarea | — | Response body. Optional. Supports variables (see [Response Body Variables](#response-body-variables)) |

**Buttons:** **Criar rota** / **Salvar alterações** (create / save), **{ } Formatar** (formats the response body JSON), **Limpar** / **Cancelar** (clear / cancel).

### Route List

| Button | Action |
| --- | --- |
| **▸** | Expands route details (ID, description and response body) |
| **✎** | Loads the route into the form for editing |
| **⏸** / **▶** | Disables / re-enables the route. A disabled route is shown faded with a "desativada" tag and is ignored by calls: the request falls through to the next matching route or gets a 404. Editing a disabled route does not re-enable it. |
| **<>** | Copies the equivalent `curl` command (with the full URL) to the clipboard |
| **✕** | Deletes the route (asks for confirmation first) |

In the list header:

- **Exportar YAML** downloads `{username}-{slug}.openapi.yaml`, an OpenAPI 3 document of the application's routes, titled with the application name and with the base URL as `servers`. Disabled while the application has no routes.
- **Atualizar** reloads the list.

---

## Calling a Route

```text
{method} http://localhost:5087/{username}-{slug}{pattern}
```

| Request | Result |
| --- | --- |
| First segment and pattern match a route | The route's status, content type and body, after the processing time |
| First segment belongs to an application but no route matches | `404 Not Found` |
| First segment does not belong to any application | Falls through to the UI (login redirect or "Página não encontrada") |

Routes of one application are never matched through another application's URL. The first segment is case-insensitive.

---

## Pattern Matching Rules

After the `/{username}-{slug}` segment is removed from the path (an empty path becomes `/`), matching checks, in this order:

1. **HTTP method** — must be equal (case-insensitive)
2. **Path** — must match the pattern (case-insensitive)
3. **Query string** — if the pattern has a query, all declared parameters must be present in the request, with the correct type when one is declared

Extra query string parameters in the request are ignored. When more than one route matches, the first one created wins.

### Path Wildcards

| Syntax | Matches | Pattern example | URL example |
| --- | --- | --- | --- |
| `*` | Any value in a segment | `/v*/users` | `/v1/users`, `/v2/users` |
| `{guid}` or `{uuid}` | UUID/GUID | `/users/{guid}` | `/users/3fa85f64-...` |
| `{int}`, `{long}`, `{number}` | Integer (positive or negative) | `/items/{int}` | `/items/42`, `/items/-1` |
| `{date}` | Date in `YYYY-MM-DD` format | `/events/{date}` | `/events/2024-12-31` |
| `{datetime}` | ISO 8601 date/time | `/logs/{datetime}` | `/logs/2024-12-31T10:00:00Z` |
| `{name}` | Any non-empty segment (any other name) | `/users/{id}` | `/users/abc`, `/users/123` |
| `{type?}` | `?` suffix makes the segment optional | `/users/{guid?}` | `/users` or `/users/3fa85f64-...` |

### Query String Validation

Use the `[type]` syntax in query parameter values:

```text
/v1/movies?year=[int]&genre=[string]&id=[guid]
```

| Type | Validates |
| --- | --- |
| `[guid]` | Valid UUID/GUID |
| `[int]` | Integer number |
| `[date]` or `[datetime]` | Parseable date |
| `[string]` | Any value (only presence is checked) |

A literal value (e.g. `?status=active`) is not compared — only the presence of the parameter is checked.

**Examples** (application `store` of user `alice`):

```text
/api/orders                          → GET /alice-store/api/orders
/api/orders/{guid}                   → GET /alice-store/api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6
/v*/products/{int}                   → GET /alice-store/v1/products/10, GET /alice-store/v2/products/99
/reports/{date}                      → GET /alice-store/reports/2024-01-15
/search?q=[string]&page=[int]        → GET /alice-store/search?q=hello&page=2
/users/{uuid?}                       → GET /alice-store/users  or  GET /alice-store/users/3fa85f64-...
/                                    → GET /alice-store
```

---

## Response Body Variables

The response body supports variable substitution using the `{{expression}}` syntax. Values are resolved on every request.

| Expression | Result |
| --- | --- |
| `{{uuid}}` | Randomly generated UUID |
| `{{$.property}}` | Value of the `property` field from the request body (JSON) |
| `{{$.user.address.city}}` | Supports nested paths |
| `{{$.items[0]}}` | Supports array index access |

**Rules:**

- If the JSONPath property does not exist in the body → replaced with an empty string
- If the request body is absent or not valid JSON → `{{$.x}}` expressions are kept literally in the output
- Unrecognized expressions → kept literally in the output

**Example:**

Received request body:

```json
{ "name": "Alice", "role": "admin" }
```

Configured response body:

```json
{
  "id": "{{uuid}}",
  "name": "{{$.name}}",
  "role": "{{$.role}}",
  "department": "{{$.department}}"
}
```

Returned response:

```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "name": "Alice",
  "role": "admin",
  "department": ""
}
```

---

## Use Cases

All examples assume user `alice` with an application whose slug is `store` (base URL `/alice-store`). Create each route in the Route Manager with the values shown.

### Simulate resource creation with a dynamic ID

| Field | Value |
| --- | --- |
| Method | `POST` |
| Pattern | `/api/users` |
| Status Code | `201` |
| Response body | `{ "id": "{{uuid}}", "name": "{{$.name}}", "email": "{{$.email}}" }` |

```bash
curl -X POST http://localhost:5087/alice-store/api/users \
  -H "Content-Type: application/json" \
  -d '{ "name": "Alice", "email": "alice@example.com" }'
```

```json
{ "id": "a3f2c1d4-...", "name": "Alice", "email": "alice@example.com" }
```

### Simulate timeout / slow endpoint

| Field | Value |
| --- | --- |
| Method | `GET` |
| Pattern | `/api/reports` |
| Status Code | `504` |
| Processing time | `60000` |
| Response body | `{ "error": "Gateway Timeout" }` |

### Simulate authentication error

| Field | Value |
| --- | --- |
| Method | `GET` |
| Pattern | `/api/protected` |
| Status Code | `401` |
| Response body | `{ "error": "Unauthorized" }` |

### Endpoint with typed URL parameter

| Field | Value |
| --- | --- |
| Method | `GET` |
| Pattern | `/api/orders/{guid}` |
| Response body | `{ "id": "{{uuid}}", "status": "shipped" }` |

Accepts: `GET /alice-store/api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6`

Rejects with 404: `GET /alice-store/api/orders/abc` (not a valid UUID)

### Endpoint with typed query string

| Field | Value |
| --- | --- |
| Method | `GET` |
| Pattern | `/api/products?category=[string]&page=[int]` |
| Response body | `{ "items": [] }` |

Accepts: `GET /alice-store/api/products?category=electronics&page=1`

Rejects with 404: `GET /alice-store/api/products?category=electronics&page=abc` (page is not an integer)

### Wildcard version endpoint

| Field | Value |
| --- | --- |
| Method | `GET` |
| Pattern | `/v*/health` |
| Response body | `{ "status": "ok" }` |

Accepts: `/alice-store/v1/health`, `/alice-store/v2/health`, `/alice-store/v10/health`

---

## Reserved Routes

The following first path segments belong to the system. They never reach the route dispatcher, and an application whose `{username}-{slug}` would equal one of them is refused:

| Segment | Usage |
| --- | --- |
| `/account` | Login, registration and logout |
| `/apps` | Route Manager pages |
| `/signin-github` | GitHub OAuth callback |
| `/_blazor` | Blazor SignalR hub |
| `/_framework` | Blazor framework files |
| `/_content` | Component static content |
| `/css` | Stylesheets |
| `/js` | Scripts |
| `/favicon.ico` | Site icon |

---

## Persistence

Users, applications, routes and the ASP.NET Core Data Protection keys (which sign the login cookie) are stored in a SQLite database at `Imockey/data/mocks.db`, created automatically on startup. Deleting an application deletes its routes.

The location can be changed with the `Database:Path` setting (environment variable `Database__Path`); relative paths are resolved against the application folder. The database runs in WAL mode, so `mocks.db-wal` and `mocks.db-shm` files may appear next to it.

To reset everything, stop the server and delete `mocks.db` (and `mocks.db-wal` / `mocks.db-shm`, if present). Existing sessions become invalid and users must register again.

---

## Deployment

The repository includes a `Dockerfile` that runs imockey under [Litestream](https://litestream.io), which replicates `mocks.db` to Azure Blob Storage and restores it on every container start. This makes it possible to run on platforms that scale to zero.

- The image listens on port `8080` and stores the database at `/data/mocks.db`.
- Replication is enabled when `AZURE_STORAGE_ACCOUNT` and `LITESTREAM_AZURE_ACCOUNT_KEY` are set; without them the app runs without replication.
- Run **at most one replica**: SQLite + Litestream assume a single writing process.
- The `Container` workflow publishes the image to GitHub Container Registry (`ghcr.io/phduarte/imockey`, tags `latest` and the short commit SHA) after the `.NET` workflow passes on `master`.

The step-by-step guide for Azure Container Apps, aimed at near-zero cost, is in [docs/deploy-azure-container-apps.md](docs/deploy-azure-container-apps.md) (Portuguese).

To run the image locally, without replication, using the same `Imockey/data` folder as local development (run from the repository root):

```bash
docker build -t imockey .
docker run --rm -p 8080:8080 -v "$PWD/Imockey/data:/data" imockey
```

On Windows PowerShell, use `-v "${PWD}\Imockey\data:/data"`. Mount the **folder**, not the `mocks.db` file (SQLite in WAL mode writes `mocks.db-wal`/`mocks.db-shm` next to it). Don't run the container and `dotnet run` against the same folder at the same time. Without `-v`, the data is lost when the container is removed.

---

## Development

| Environment | URL |
| --- | --- |
| HTTP | `http://localhost:5087` |
| HTTPS | `https://localhost:7253` |

```bash
dotnet build Imockey        # stop the server first so the build can overwrite the executable
dotnet test Imockey.Tests   # xUnit + FluentAssertions, against a temporary SQLite database
```

Contributor guidelines (architecture, business rules and how to add features) are in [CLAUDE.md](CLAUDE.md) (Portuguese).

---

## Market Analysis

A feature analysis, a benchmark against similar products and suggested improvements for a paid plan are in [docs/analise-de-mercado.md](docs/analise-de-mercado.md) (Portuguese).

---

## Technologies

- .NET 10 / ASP.NET Core
- Blazor Server (management UI) and Razor Pages (account pages)
- Cookie authentication + GitHub OAuth ([AspNet.Security.OAuth.GitHub](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers))
- SQLite via Microsoft.Data.Sqlite (plain ADO.NET)
- Newtonsoft.Json (JSONPath in response templating)
- YamlDotNet (OpenAPI export)
- Docker + Litestream (replication to Azure Blob Storage)
- xUnit + FluentAssertions (tests)
