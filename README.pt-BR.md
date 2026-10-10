# RestMock

[![.NET](https://github.com/phduarte/restmock/actions/workflows/dotnet.yml/badge.svg)](https://github.com/phduarte/restmock/actions/workflows/dotnet.yml)

Servidor HTTP para criação de endpoints mockados. Útil em testes de integração, testes de estresse, simulação de caos, debugging e desenvolvimento desacoplado.

## Sumário

- [Conceitos](#conceitos)
- [Início rápido](#início-rápido)
- [Interface visual (Blazor UI)](#interface-visual-blazor-ui)
- [API REST](#api-rest)
- [Regras de pattern matching](#regras-de-pattern-matching)
- [Variáveis no response body](#variáveis-no-response-body)
- [Casos de uso](#casos-de-uso)
- [Tecnologias](#tecnologias)

---

## Conceitos

**Mock** — Cópia não funcional de um endpoint real. Retorna respostas configuradas sem nenhuma lógica de negócio.

**Pattern** — Endereço do endpoint mockado (sem o host), podendo conter curingas e tipos de validação.

**Processing time** — Atraso artificial aplicado antes de responder, útil para simular latência ou timeout.

---

## Início rápido

```bash
# clonar e executar
git clone https://github.com/phduarte/restmock
cd restmock
run.bat
```

O `run.bat` inicia o servidor e abre automaticamente o navegador em `http://localhost:5087`.

URLs disponíveis após iniciar:

| Endereço | Descrição |
| --- | --- |
| `http://localhost:5087/` | Interface visual (Blazor UI) |
| `http://localhost:5087/client` | Interface visual (alias) |
| `http://localhost:5087/swagger` | Documentação da API (modo desenvolvimento) |

> **Atenção:** os dados são armazenados apenas em memória. Todos os mocks são perdidos ao reiniciar o servidor.

---

## Interface visual (Blazor UI)

Acesse `http://localhost:5087` para gerenciar os mocks pelo navegador.

### Layout

A tela é dividida em duas colunas:

- **Esquerda** — formulário para criação de novos mocks
- **Direita** — lista de mocks ativos com opções de detalhe e exclusão

A lista atualiza automaticamente em tempo real via Blazor Server (SignalR).

### Formulário de criação

| Campo | Tipo | Padrão | Descrição |
| --- | --- | --- | --- |
| Método HTTP | dropdown | GET | GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS |
| Pattern (URL) | texto | — | Endereço do endpoint. Obrigatório. Suporta curingas (ver [Regras de pattern matching](#regras-de-pattern-matching)) |
| Descrição | texto | — | Texto livre para documentar o propósito do mock. Opcional. |
| Status Code | número | 200 | Código HTTP de resposta. Entre 100 e 599 |
| Processing time (ms) | número | 0 | Atraso em milissegundos antes de responder |
| Content-Type | dropdown | application/json | Tipo MIME da resposta |
| Response body | textarea | — | Corpo da resposta. Opcional. Suporta variáveis (ver [Variáveis no response body](#variáveis-no-response-body)) |

**Botões:**

- **Criar mock** — valida e salva o endpoint
- **{ } Formatar** — formata o JSON do response body
- **Limpar** — reinicia o formulário para os valores padrão

**Validações:**

- Pattern é obrigatório
- Status Code deve estar entre 100 e 599

### Lista de mocks ativos

Exibe todos os endpoints cadastrados em tabela com as colunas: Método, Pattern, Status, Content-Type, Delay e Ações.

| Botão | Ação |
| --- | --- |
| **▸** | Expande os detalhes do mock (ID, descrição e response body) |
| **✎** | Carrega o mock no formulário para edição |
| **<>** | Copia o comando `curl` equivalente para a área de transferência |
| **✕** | Exclui o mock (pede confirmação antes) |

---

## API REST

Todos os endpoints de gerenciamento ficam sob o prefixo `/mocks`.

### Modelo

| Campo | Tipo | Padrão | Descrição |
| --- | --- | --- | --- |
| `id` | uuid | auto | Identificador único. Gerado automaticamente. |
| `httpMethod` | string | `"GET"` | Método HTTP: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS |
| `pattern` | string | — | Endereço do endpoint mockado (sem host) |
| `statusCode` | int | `200` | Código HTTP de resposta (100–599) |
| `processingTime` | int | `0` | Atraso em milissegundos (mínimo 0) |
| `contentType` | string | `"application/json"` | Tipo MIME da resposta |
| `responseBody` | object | `null` | Corpo da resposta. Pode ser string, JSON ou null |
| `description` | string | `null` | Descrição opcional do mock. Puramente informativa. |

### `POST /mocks` — criar mock

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

**Resposta:** `201 Created` com o mock criado no body e o header `Location: /mocks/{id}`.

### `GET /mocks` — listar todos

```http
GET /mocks
```

**Resposta:** `200 OK` com array de todos os mocks.

### `GET /mocks/{id}` — buscar por ID

```http
GET /mocks/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Resposta:** `200 OK` com o mock, ou `404 Not Found`.

### `PUT /mocks/{id}` — editar mock

```http
PUT /mocks/3fa85f64-5717-4562-b3fc-2c963f66afa6
Content-Type: application/json

{
  "httpMethod": "GET",
  "pattern": "/api/users",
  "statusCode": 200,
  "processingTime": 0,
  "contentType": "application/json",
  "description": "Lista todos os usuários",
  "responseBody": { "items": [] }
}
```

**Resposta:** `200 OK` com o mock atualizado, ou `404 Not Found`.

### `DELETE /mocks/{id}` — excluir

```http
DELETE /mocks/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Resposta:** `204 No Content`, ou `404 Not Found`.

---

## Regras de pattern matching

O matching de uma requisição com um mock verifica, nessa ordem:

1. **Método HTTP** — deve ser igual (case-insensitive)
2. **Path** — deve corresponder ao padrão (case-insensitive)
3. **Query string** — se o pattern tiver query, todos os parâmetros declarados devem estar presentes na requisição com os tipos corretos

Parâmetros extras na query string da requisição são ignorados.

### Curingas no path

| Sintaxe | Corresponde a | Exemplo de pattern | Exemplo de URL |
| --- | --- | --- | --- |
| `*` | Qualquer valor em um segmento | `/v*/users` | `/v1/users`, `/v2/users` |
| `{guid}` ou `{uuid}` | UUID/GUID | `/users/{guid}` | `/users/3fa85f64-...` |
| `{int}`, `{long}`, `{number}` | Número inteiro (positivo ou negativo) | `/items/{int}` | `/items/42`, `/items/-1` |
| `{date}` | Data no formato `YYYY-MM-DD` | `/events/{date}` | `/events/2024-12-31` |
| `{datetime}` | Data/hora ISO 8601 | `/logs/{datetime}` | `/logs/2024-12-31T10:00:00Z` |
| `{nome}` | Qualquer segmento não vazio (nome qualquer) | `/users/{id}` | `/users/abc`, `/users/123` |
| `{tipo?}` | Sufixo `?` torna o segmento opcional | `/users/{guid?}` | `/users` ou `/users/3fa85f64-...` |

### Validação de query string

Use a sintaxe `[tipo]` nos valores dos parâmetros de query:

```text
/v1/movies?year=[int]&genre=[string]&id=[guid]
```

| Tipo | Valida |
| --- | --- |
| `[guid]` | UUID/GUID válido |
| `[int]` | Número inteiro |
| `[date]` ou `[datetime]` | Data parseável |
| `[string]` | Qualquer valor não vazio |

**Exemplos:**

```text
/api/orders                          → GET /api/orders
/api/orders/{guid}                   → GET /api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6
/v*/products/{int}                   → GET /v1/products/10, GET /v2/products/99
/reports/{date}                      → GET /reports/2024-01-15
/search?q=[string]&page=[int]        → GET /search?q=hello&page=2
/users/{uuid?}                       → GET /users  ou  GET /users/3fa85f64-...
```

---

## Variáveis no response body

O response body suporta substituição de variáveis com a sintaxe `{{expressão}}`. Os valores são resolvidos a cada requisição.

| Expressão | Resultado |
| --- | --- |
| `{{uuid}}` | UUID aleatório gerado na hora |
| `{{$.propriedade}}` | Valor da propriedade `propriedade` do request body (JSON) |
| `{{$.user.address.city}}` | Suporta caminhos aninhados |
| `{{$.items[0]}}` | Suporta acesso a índices de array |

**Regras:**

- Se a propriedade do JSONPath não existir no body → substituída por string vazia
- Se o request body estiver ausente ou não for JSON válido → todas as expressões `{{$.x}}` viram string vazia
- Expressões não reconhecidas → mantidas literalmente no output

**Exemplo:**

Request body recebido:

```json
{ "name": "Alice", "role": "admin" }
```

Response body configurado:

```json
{
  "id": "{{uuid}}",
  "name": "{{$.name}}",
  "role": "{{$.role}}",
  "department": "{{$.department}}"
}
```

Response retornado:

```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "name": "Alice",
  "role": "admin",
  "department": ""
}
```

---

## Casos de uso

### Simular criação de recurso com ID dinâmico

Criar o mock via API:

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

Chamar o mock:

```http
POST /api/users
Content-Type: application/json

{ "name": "João", "email": "joao@example.com" }
```

Resposta:

```json
{
  "id": "a3f2c1d4-...",
  "name": "João",
  "email": "joao@example.com"
}
```

### Simular timeout / endpoint lento

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

### Simular erro de autenticação

```json
POST /mocks
{
  "httpMethod": "GET",
  "pattern": "/api/protected",
  "statusCode": 401,
  "responseBody": { "error": "Unauthorized" }
}
```

### Endpoint com parâmetro tipado na URL

```json
POST /mocks
{
  "httpMethod": "GET",
  "pattern": "/api/orders/{guid}",
  "statusCode": 200,
  "responseBody": { "id": "{{uuid}}", "status": "shipped" }
}
```

Aceita: `GET /api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6`

Rejeita: `GET /api/orders/abc` (não é UUID válido)

### Endpoint com query string tipada

```json
POST /mocks
{
  "httpMethod": "GET",
  "pattern": "/api/products?category=[string]&page=[int]",
  "statusCode": 200,
  "responseBody": { "items": [] }
}
```

Aceita: `GET /api/products?category=electronics&page=1`

Rejeita: `GET /api/products?category=electronics&page=abc` (page não é inteiro)

### Endpoint de versão curinga

```json
POST /mocks
{
  "httpMethod": "GET",
  "pattern": "/v*/health",
  "statusCode": 200,
  "responseBody": { "status": "ok" }
}
```

Aceita: `/v1/health`, `/v2/health`, `/v10/health`

---

## Rotas reservadas

As seguintes rotas são reservadas pelo sistema e **não podem ser mockadas**:

| Prefixo | Uso |
| --- | --- |
| `/client` | Interface Blazor Client |
| `/mocks` | API de gerenciamento |
| `/swagger` | Documentação Swagger |
| `/_blazor` | Hub SignalR do Blazor |
| `/_framework` | Arquivos de framework Blazor |
| `/_content` | Conteúdo estático de componentes |
| `/css` | Estilos |
| `/js` | Scripts |
| `/favicon` | Ícone do site |

---

## Tecnologias

- .NET 10 / ASP.NET Core
- Blazor Server (interface visual)
- Newtonsoft.Json (JSONPath no response templating)
- Swagger / Swashbuckle 10.x (documentação da API)
