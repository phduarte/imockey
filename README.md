# RestMock

[![.NET](https://github.com/phduarte/restmock/actions/workflows/dotnet.yml/badge.svg)](https://github.com/phduarte/restmock/actions/workflows/dotnet.yml)

An HTTP server for creating mocked endpoints. Useful for integration tests, stress tests, chaos simulation, debugging, and decoupled development.

> Also available in [Português (pt-BR)](README.pt-BR.md).

## Table of Contents

- [Concepts](#concepts)
- [Quick Start](#quick-start)
- [Visual Interface (Blazor UI)](#visual-interface-blazor-ui)
- [REST API](#rest-api)
- [Pattern Matching Rules](#pattern-matching-rules)
- [Response Body Variables](#response-body-variables)
- [Use Cases](#use-cases)
- [Technologies](#technologies)

---

## Concepts

**Mock** — A non-functional copy of a real endpoint. Returns configured responses without any business logic.

**Pattern** — The address of the mocked endpoint (without the host), which may contain wildcards and validation types.

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

Available URLs after starting:

| Address | Description |
| --- | --- |
| `http://localhost:5087/` | Visual interface (Blazor UI) |
| `http://localhost:5087/client` | Visual interface (alias) |
| `http://localhost:5087/swagger` | API documentation (development mode) |

> **Note:** Data is stored in memory only. All mocks are lost when the server restarts.

---

## Visual Interface (Blazor UI)

Go to `http://localhost:5087` to manage mocks in the browser.

### Layout

The screen is split into two columns:

- **Left** — form for creating new mocks
- **Right** — list of active mocks with detail and delete options

The list updates automatically in real time via Blazor Server (SignalR).

### Creation Form

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| HTTP Method | dropdown | GET | GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS |
| Pattern (URL) | text | — | Endpoint address. Required. Supports wildcards (see [Pattern Matching Rules](#pattern-matching-rules)) |
| Description | text | — | Free text to document the purpose of the mock. Optional. |
| Status Code | number | 200 | HTTP response code. Between 100 and 599 |
| Processing time (ms) | number | 0 | Delay in milliseconds before responding |
| Content-Type | dropdown | application/json | Response MIME type |
| Response body | textarea | — | Response body. Optional. Supports variables (see [Response Body Variables](#response-body-variables)) |

**Buttons:**

- **Create mock** — validates and saves the endpoint
- **{ } Format** — formats the response body JSON
- **Clear** — resets the form to default values

**Validations:**

- Pattern is required
- Status Code must be between 100 and 599

### Active Mocks List

Displays all registered endpoints in a table with columns: Method, Pattern, Status, Content-Type, Delay, and Actions.

| Button | Action |
| --- | --- |
| **▸** | Expands mock details (ID, description, and response body) |
| **✎** | Loads the mock into the form for editing |
| **<>** | Copies the equivalent `curl` command to the clipboard |
| **✕** | Deletes the mock (asks for confirmation first) |

---

## REST API

All management endpoints are under the `/mocks` prefix.

### Model

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `id` | uuid | auto | Unique identifier. Auto-generated. |
| `httpMethod` | string | `"GET"` | HTTP method: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS |
| `pattern` | string | — | Mocked endpoint address (without host) |
| `statusCode` | int | `200` | HTTP response code (100–599) |
| `processingTime` | int | `0` | Delay in milliseconds (minimum 0) |
| `contentType` | string | `"application/json"` | Response MIME type |
| `responseBody` | object | `null` | Response body. Can be a string, JSON, or null |
| `description` | string | `null` | Optional mock description. Purely informational. |

### `POST /mocks` — create mock

```http
POST /mocks
Content-Type: application/json

{
  "httpMethod": "POST",
  "pattern": "/api/v1/users",
  "statusCode": 201,
  "processingTime": 0,
  "contentType": "application/json",
  "responseBody": {
    "id": "{{uuid}}",
    "name": "{{$.name}}"
  }
}
```

**Response:** `201 Created` with the created mock in the body and the `Location: /mocks/{id}` header.

### `GET /mocks` — list all

```http
GET /mocks
```

**Response:** `200 OK` with an array of all mocks.

### `GET /mocks/{id}` — get by ID

```http
GET /mocks/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Response:** `200 OK` with the mock, or `404 Not Found`.

### `PUT /mocks/{id}` — update mock

```http
PUT /mocks/3fa85f64-5717-4562-b3fc-2c963f66afa6
Content-Type: application/json

{
  "httpMethod": "GET",
  "pattern": "/api/users",
  "statusCode": 200,
  "processingTime": 0,
  "contentType": "application/json",
  "description": "List all users",
  "responseBody": { "items": [] }
}
```

**Response:** `200 OK` with the updated mock, or `404 Not Found`.

### `DELETE /mocks/{id}` — delete

```http
DELETE /mocks/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Response:** `204 No Content`, or `404 Not Found`.

---

## Pattern Matching Rules

Matching a request against a mock checks, in this order:

1. **HTTP method** — must be equal (case-insensitive)
2. **Path** — must match the pattern (case-insensitive)
3. **Query string** — if the pattern has a query, all declared parameters must be present in the request with the correct types

Extra query string parameters in the request are ignored.

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

**Examples:**

```text
/api/orders                          → GET /api/orders
/api/orders/{guid}                   → GET /api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6
/v*/products/{int}                   → GET /v1/products/10, GET /v2/products/99
/reports/{date}                      → GET /reports/2024-01-15
/search?q=[string]&page=[int]        → GET /search?q=hello&page=2
/users/{uuid?}                       → GET /users  or  GET /users/3fa85f64-...
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

### Simulate resource creation with a dynamic ID

Create the mock via API:

```json
POST /mocks
{
  "httpMethod": "POST",
  "pattern": "/api/users",
  "statusCode": 201,
  "contentType": "application/json",
  "responseBody": {
    "id": "{{uuid}}",
    "name": "{{$.name}}",
    "email": "{{$.email}}"
  }
}
```

Call the mock:

```http
POST /api/users
Content-Type: application/json

{ "name": "Alice", "email": "alice@example.com" }
```

Response:

```json
{
  "id": "a3f2c1d4-...",
  "name": "Alice",
  "email": "alice@example.com"
}
```

### Simulate timeout / slow endpoint

```json
POST /mocks
{
  "httpMethod": "GET",
  "pattern": "/api/reports",
  "statusCode": 504,
  "processingTime": 60000,
  "responseBody": { "error": "Gateway Timeout" }
}
```

### Simulate authentication error

```json
POST /mocks
{
  "httpMethod": "GET",
  "pattern": "/api/protected",
  "statusCode": 401,
  "responseBody": { "error": "Unauthorized" }
}
```

### Endpoint with typed URL parameter

```json
POST /mocks
{
  "httpMethod": "GET",
  "pattern": "/api/orders/{guid}",
  "statusCode": 200,
  "responseBody": { "id": "{{uuid}}", "status": "shipped" }
}
```

Accepts: `GET /api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6`

Rejects: `GET /api/orders/abc` (not a valid UUID)

### Endpoint with typed query string

```json
POST /mocks
{
  "httpMethod": "GET",
  "pattern": "/api/products?category=[string]&page=[int]",
  "statusCode": 200,
  "responseBody": { "items": [] }
}
```

Accepts: `GET /api/products?category=electronics&page=1`

Rejects: `GET /api/products?category=electronics&page=abc` (page is not an integer)

### Wildcard version endpoint

```json
POST /mocks
{
  "httpMethod": "GET",
  "pattern": "/v*/health",
  "statusCode": 200,
  "responseBody": { "status": "ok" }
}
```

Accepts: `/v1/health`, `/v2/health`, `/v10/health`

---

## Reserved Routes

The following routes are reserved by the system and **cannot be mocked**:

| Prefix | Usage |
| --- | --- |
| `/client` | Blazor Client interface |
| `/mocks` | Management API |
| `/swagger` | Swagger documentation |
| `/_blazor` | Blazor SignalR hub |
| `/_framework` | Blazor framework files |
| `/_content` | Component static content |
| `/css` | Stylesheets |
| `/js` | Scripts |
| `/favicon` | Site icon |

---

## Technologies

- .NET 10 / ASP.NET Core
- Blazor Server (visual interface)
- Newtonsoft.Json (JSONPath in response templating)
- Swagger / Swashbuckle 10.x (API documentation)
