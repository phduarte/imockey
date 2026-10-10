# RestMock

[![.NET](https://github.com/phduarte/restmock/actions/workflows/dotnet.yml/badge.svg)](https://github.com/phduarte/restmock/actions/workflows/dotnet.yml)

An HTTP server for creating mocked endpoints. Useful for integration tests, stress tests, chaos simulation, debugging, and decoupled development.

> Also available in [Português (pt-BR)](README.pt-BR.md).

## Table of Contents

- [Concepts](#concepts)
- [Quick Start](#quick-start)
- [Accounts and Sign-in](#accounts-and-sign-in)
- [Applications](#applications)
- [Mock Manager](#mock-manager)
- [Calling a Mock](#calling-a-mock)
- [Pattern Matching Rules](#pattern-matching-rules)
- [Response Body Variables](#response-body-variables)
- [Use Cases](#use-cases)
- [Reserved Routes](#reserved-routes)
- [Persistence](#persistence)
- [Technologies](#technologies)

---

## Concepts

**User** — Someone who signs in to RestMock. Each user only sees and manages their own applications.

**Application** — A group of mocks that simulates one real API. A user can have any number of applications. Each one has a **slug**, unique across all users, that becomes the first segment of its mocks' URLs.

**Mock** — A non-functional copy of a real endpoint. Returns configured responses without any business logic. Every mock belongs to one application.

**Pattern** — The address of the mocked endpoint inside its application (without host or slug). It may contain wildcards and validation types.

**Processing time** — An artificial delay applied before responding, useful for simulating latency or timeout.

---

## Quick Start

```bash
# clone and run
git clone https://github.com/phduarte/restmock
cd restmock
run.bat
```

`run.bat` starts the server and automatically opens the browser at `http://localhost:5087`.

1. Create an account at `/account/register` (or sign in with GitHub, if configured).
2. Create an application, for example **Store API** with slug `store`.
3. Open it and create a mock, for example `GET /v1/products`.
4. Call it: `curl http://localhost:5087/store/v1/products`.

---

## Accounts and Sign-in

| Address | Description |
| --- | --- |
| `/account/login` | Sign in with username and password, or with GitHub |
| `/account/register` | Create an account with username and password |

Every UI page requires a signed-in user; anonymous visitors are redirected to the login page. Mock calls do **not** require sign-in.

### Username and password

- Username: 3 to 32 characters — letters, numbers, `.`, `_` or `-` (case-insensitive and unique)
- Password: at least 8 characters, stored as a salted hash (ASP.NET Core Identity `PasswordHasher`)
- The session cookie lasts 14 days (sliding)

### GitHub

The **Sign in with GitHub** button only appears when an OAuth App is configured:

1. Create an OAuth App at <https://github.com/settings/developers> with callback URL `http://localhost:5087/signin-github` (adjust host/port).
2. Store the credentials outside the repository, e.g. with user-secrets:

   ```bash
   dotnet user-secrets --project RestMock init
   dotnet user-secrets --project RestMock set "Authentication:GitHub:ClientId" "<client id>"
   dotnet user-secrets --project RestMock set "Authentication:GitHub:ClientSecret" "<client secret>"
   ```

   Or use the environment variables `Authentication__GitHub__ClientId` and `Authentication__GitHub__ClientSecret`.

The first GitHub sign-in creates a RestMock user linked to that GitHub account. GitHub users and username/password users are separate accounts.

---

## Applications

After signing in, the home page (`/`) lists your applications and has a form to create a new one.

| Field | Required | Description |
| --- | --- | --- |
| Name | yes | Display name |
| Slug | no | First URL segment of the mocks. Lowercase letters, numbers and `-`, up to 50 characters. Generated from the name when empty (`Payment Service` → `payment-service`). Must be unique across all users and cannot be a [reserved route](#reserved-routes). |
| Description | no | Free text |

The list shows each application's base URL, number of mocks and creation date. Click the name (or **→**) to open its Mock Manager. **✕** deletes the application **and all of its mocks**.

---

## Mock Manager

Opened at `/apps/{applicationId}`. Only the owner can open it; anyone else sees "application not found".

The header shows the application's base URL (`http://localhost:5087/{slug}/...`). The screen is split into two columns:

- **Left** — form for creating or editing mocks
- **Right** — list of the application's mocks

The list updates automatically in real time via Blazor Server (SignalR).

### Form

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| Description | text | — | Free text to document the purpose of the mock. Optional. |
| HTTP Method | dropdown | GET | GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS |
| Pattern (URL) | text | — | Endpoint address inside the application. Required. Supports wildcards (see [Pattern Matching Rules](#pattern-matching-rules)) |
| Status Code | number | 200 | HTTP response code. Between 100 and 599 |
| Processing time (ms) | number | 0 | Delay in milliseconds before responding |
| Content-Type | dropdown | application/json | Response MIME type |
| Response body | textarea | — | Response body. Optional. Supports variables (see [Response Body Variables](#response-body-variables)) |

**Buttons:** **Create mock** / **Save changes**, **{ } Format** (formats the response body JSON), **Clear** / **Cancel**.

### Mock List

| Button | Action |
| --- | --- |
| **▸** | Expands mock details (ID, description, and response body) |
| **✎** | Loads the mock into the form for editing |
| **<>** | Copies the equivalent `curl` command (with the slug) to the clipboard |
| **✕** | Deletes the mock (asks for confirmation first) |

**Export YAML** downloads an OpenAPI 3 document of the application's mocks, titled with the application name and with the base URL as `servers`.

---

## Calling a Mock

```text
{method} http://localhost:5087/{slug}{pattern}
```

| Request | Result |
| --- | --- |
| Slug and pattern match a mock | The mock's status, content type and body |
| Slug belongs to an application but no mock matches | `404 Not Found` |
| Slug does not belong to any application | Falls through to the UI (login redirect or "page not found") |

Mocks of one application are never matched through another application's slug. The slug is case-insensitive.

---

## Pattern Matching Rules

After the slug is removed from the path, matching checks, in this order:

1. **HTTP method** — must be equal (case-insensitive)
2. **Path** — must match the pattern (case-insensitive)
3. **Query string** — if the pattern has a query, all declared parameters must be present in the request with the correct types

Extra query string parameters in the request are ignored. When more than one mock matches, the first one created wins.

### Path Wildcards

| Syntax | Matches | Pattern example | URL example |
| --- | --- | --- | --- |
| `*` | Any value in a segment | `/v*/users` | `/v1/users`, `/v2/users` |
| `{guid}` or `{uuid}` | UUID/GUID | `/users/{guid}` | `/users/3fa85f64-...` |
| `{int}`, `{long}`, `{number}` | Integer (positive or negative) | `/items/{int}` | `/items/42`, `/items/-1` |
| `{date}` | Date in `YYYY-MM-DD` format | `/events/{date}` | `/events/2024-12-31` |
| `{datetime}` | ISO 8601 date/time | `/logs/{datetime}` | `/logs/2024-12-31T10:00:00Z` |
| `{name}` | Any non-empty segment (any name) | `/users/{id}` | `/users/abc`, `/users/123` |
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
| `[string]` | Any non-empty value |

**Examples** (application slug `store`):

```text
/api/orders                          → GET /store/api/orders
/api/orders/{guid}                   → GET /store/api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6
/v*/products/{int}                   → GET /store/v1/products/10, GET /store/v2/products/99
/reports/{date}                      → GET /store/reports/2024-01-15
/search?q=[string]&page=[int]        → GET /store/search?q=hello&page=2
/users/{uuid?}                       → GET /store/users  or  GET /store/users/3fa85f64-...
/                                    → GET /store
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
- If the request body is absent or not valid JSON → all `{{$.x}}` expressions become empty strings
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

All examples assume an application with slug `store`. Create each mock in the Mock Manager with the values shown.

### Simulate resource creation with a dynamic ID

| Field | Value |
| --- | --- |
| Method | `POST` |
| Pattern | `/api/users` |
| Status Code | `201` |
| Response body | `{ "id": "{{uuid}}", "name": "{{$.name}}", "email": "{{$.email}}" }` |

```bash
curl -X POST http://localhost:5087/store/api/users \
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

Accepts: `GET /store/api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6`

Rejects with 404: `GET /store/api/orders/abc` (not a valid UUID)

### Endpoint with typed query string

| Field | Value |
| --- | --- |
| Method | `GET` |
| Pattern | `/api/products?category=[string]&page=[int]` |
| Response body | `{ "items": [] }` |

Accepts: `GET /store/api/products?category=electronics&page=1`

Rejects with 404: `GET /store/api/products?category=electronics&page=abc` (page is not an integer)

### Wildcard version endpoint

| Field | Value |
| --- | --- |
| Method | `GET` |
| Pattern | `/v*/health` |
| Response body | `{ "status": "ok" }` |

Accepts: `/store/v1/health`, `/store/v2/health`, `/store/v10/health`

---

## Reserved Routes

The following first path segments belong to the system. They never reach the mock dispatcher and **cannot be used as application slugs**:

| Segment | Usage |
| --- | --- |
| `/account` | Login, registration and logout |
| `/apps` | Mock Manager pages |
| `/signin-github` | GitHub OAuth callback |
| `/_blazor` | Blazor SignalR hub |
| `/_framework` | Blazor framework files |
| `/_content` | Component static content |
| `/css` | Stylesheets |
| `/js` | Scripts |
| `/favicon.ico` | Site icon |

---

## Persistence

Users, applications and mocks are stored in a SQLite database at `RestMock/mocks.db`, created automatically on startup. Deleting an application deletes its mocks. To reset everything, stop the server and delete the file.

---

## Technologies

- .NET 10 / ASP.NET Core
- Blazor Server (visual interface) and Razor Pages (account pages)
- Cookie authentication + GitHub OAuth ([AspNet.Security.OAuth.GitHub](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers))
- SQLite via Microsoft.Data.Sqlite
- Newtonsoft.Json (JSONPath in response templating)
- YamlDotNet (OpenAPI export)
