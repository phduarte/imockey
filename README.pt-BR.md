# RestMock

[![.NET](https://github.com/phduarte/restmock/actions/workflows/dotnet.yml/badge.svg)](https://github.com/phduarte/restmock/actions/workflows/dotnet.yml)

Servidor HTTP para criação de endpoints mockados. Útil em testes de integração, testes de estresse, simulação de caos, debugging e desenvolvimento desacoplado.

## Sumário

- [Conceitos](#conceitos)
- [Início rápido](#início-rápido)
- [Contas e login](#contas-e-login)
- [Aplicações](#aplicações)
- [Gerenciador de Mocks](#gerenciador-de-mocks)
- [Chamando um mock](#chamando-um-mock)
- [Regras de pattern matching](#regras-de-pattern-matching)
- [Variáveis no response body](#variáveis-no-response-body)
- [Casos de uso](#casos-de-uso)
- [Rotas reservadas](#rotas-reservadas)
- [Persistência](#persistência)
- [Tecnologias](#tecnologias)

---

## Conceitos

**Usuário** — Quem faz login no RestMock. Cada usuário só vê e gerencia as próprias aplicações.

**Aplicação** — Um conjunto de mocks que simula uma API real. Um usuário pode ter quantas aplicações quiser. Cada uma tem um **slug**, único entre todos os usuários, que vira o primeiro segmento da URL dos seus mocks.

**Mock** — Cópia não funcional de um endpoint real. Retorna respostas configuradas sem nenhuma lógica de negócio. Todo mock pertence a uma aplicação.

**Pattern** — Endereço do endpoint mockado dentro da aplicação (sem host nem slug), podendo conter curingas e tipos de validação.

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

1. Crie uma conta em `/account/register` (ou entre com GitHub, se configurado).
2. Crie uma aplicação, por exemplo **Loja API** com slug `loja`.
3. Abra a aplicação e crie um mock, por exemplo `GET /v1/products`.
4. Chame o mock: `curl http://localhost:5087/loja/v1/products`.

---

## Contas e login

| Endereço | Descrição |
| --- | --- |
| `/account/login` | Login com usuário e senha, ou com GitHub |
| `/account/register` | Cadastro com usuário e senha |

Todas as páginas da interface exigem usuário logado; visitantes anônimos são redirecionados para o login. As chamadas aos mocks **não** exigem login.

### Usuário e senha

- Usuário: de 3 a 32 caracteres — letras, números, `.`, `_` ou `-` (único, sem diferenciar maiúsculas/minúsculas)
- Senha: no mínimo 8 caracteres, armazenada como hash com salt (`PasswordHasher` do ASP.NET Core Identity)
- O cookie de sessão dura 14 dias (renovado com o uso)

### GitHub

O botão **Entrar com GitHub** só aparece quando há um OAuth App configurado:

1. Crie um OAuth App em <https://github.com/settings/developers> com a callback URL `http://localhost:5087/signin-github` (ajuste host/porta).
2. Guarde as credenciais fora do repositório, por exemplo com user-secrets:

   ```bash
   dotnet user-secrets --project RestMock init
   dotnet user-secrets --project RestMock set "Authentication:GitHub:ClientId" "<client id>"
   dotnet user-secrets --project RestMock set "Authentication:GitHub:ClientSecret" "<client secret>"
   ```

   Ou use as variáveis de ambiente `Authentication__GitHub__ClientId` e `Authentication__GitHub__ClientSecret`.

O primeiro login com GitHub cria um usuário do RestMock vinculado àquela conta do GitHub. Usuários do GitHub e usuários com senha são contas separadas.

---

## Aplicações

Depois do login, a página inicial (`/`) lista suas aplicações e tem um formulário para criar uma nova.

| Campo | Obrigatório | Descrição |
| --- | --- | --- |
| Nome | sim | Nome de exibição |
| Slug | não | Primeiro segmento da URL dos mocks. Letras minúsculas, números e `-`, até 50 caracteres. Gerado a partir do nome quando vazio (`Serviço de Pagamentos` → `servico-de-pagamentos`). Deve ser único entre todos os usuários e não pode ser uma [rota reservada](#rotas-reservadas). |
| Descrição | não | Texto livre |

A lista mostra a URL base, a quantidade de mocks e a data de criação de cada aplicação. Clique no nome (ou em **→**) para abrir o Gerenciador de Mocks. **✕** exclui a aplicação **e todos os seus mocks**.

---

## Gerenciador de Mocks

Aberto em `/apps/{applicationId}`. Só o dono consegue abrir; qualquer outro usuário vê "Aplicação não encontrada".

O cabeçalho mostra a URL base da aplicação (`http://localhost:5087/{slug}/...`). A tela é dividida em duas colunas:

- **Esquerda** — formulário de criação e edição de mocks
- **Direita** — lista dos mocks da aplicação

A lista é atualizada automaticamente em tempo real via Blazor Server (SignalR).

### Formulário

| Campo | Tipo | Padrão | Descrição |
| --- | --- | --- | --- |
| Descrição | texto | — | Texto livre para documentar o propósito do mock. Opcional. |
| Método HTTP | dropdown | GET | GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS |
| Pattern (URL) | texto | — | Endereço do endpoint dentro da aplicação. Obrigatório. Suporta curingas (ver [Regras de pattern matching](#regras-de-pattern-matching)) |
| Status Code | número | 200 | Código HTTP de resposta. Entre 100 e 599 |
| Processing time (ms) | número | 0 | Atraso em milissegundos antes de responder |
| Content-Type | dropdown | application/json | Tipo MIME da resposta |
| Response body | textarea | — | Corpo da resposta. Opcional. Suporta variáveis (ver [Variáveis no response body](#variáveis-no-response-body)) |

**Botões:** **Criar mock** / **Salvar alterações**, **{ } Formatar** (formata o JSON do response body), **Limpar** / **Cancelar**.

### Lista de mocks

| Botão | Ação |
| --- | --- |
| **▸** | Expande os detalhes do mock (ID, descrição e response body) |
| **✎** | Carrega o mock no formulário para edição |
| **<>** | Copia o comando `curl` equivalente (com o slug) para a área de transferência |
| **✕** | Exclui o mock (pede confirmação antes) |

**Exportar YAML** baixa um documento OpenAPI 3 com os mocks da aplicação, com o nome da aplicação como título e a URL base em `servers`.

---

## Chamando um mock

```text
{método} http://localhost:5087/{slug}{pattern}
```

| Requisição | Resultado |
| --- | --- |
| Slug e pattern casam com um mock | Status, content type e body do mock |
| Slug pertence a uma aplicação, mas nenhum mock casa | `404 Not Found` |
| Slug não pertence a nenhuma aplicação | Segue para a interface (redirect para o login ou "página não encontrada") |

Mocks de uma aplicação nunca são encontrados pelo slug de outra. O slug não diferencia maiúsculas de minúsculas.

---

## Regras de pattern matching

Depois de remover o slug do path, o matching verifica, nesta ordem:

1. **Método HTTP** — deve ser igual (case-insensitive)
2. **Path** — deve corresponder ao pattern (case-insensitive)
3. **Query string** — se o pattern declarar query, todos os parâmetros declarados devem estar presentes na requisição com o tipo correto

Parâmetros extras na query string da requisição são ignorados. Quando mais de um mock casa, vence o criado primeiro.

### Curingas no path

| Sintaxe | Corresponde a | Exemplo de pattern | Exemplo de URL |
| --- | --- | --- | --- |
| `*` | Qualquer valor em um segmento | `/v*/users` | `/v1/users`, `/v2/users` |
| `{guid}` ou `{uuid}` | UUID/GUID | `/users/{guid}` | `/users/3fa85f64-...` |
| `{int}`, `{long}`, `{number}` | Número inteiro (positivo ou negativo) | `/items/{int}` | `/items/42`, `/items/-1` |
| `{date}` | Data no formato `YYYY-MM-DD` | `/events/{date}` | `/events/2024-12-31` |
| `{datetime}` | Data/hora ISO 8601 | `/logs/{datetime}` | `/logs/2024-12-31T10:00:00Z` |
| `{nome}` | Qualquer segmento não vazio (qualquer nome) | `/users/{id}` | `/users/abc`, `/users/123` |
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

**Exemplos** (aplicação com slug `loja`):

```text
/api/orders                          → GET /loja/api/orders
/api/orders/{guid}                   → GET /loja/api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6
/v*/products/{int}                   → GET /loja/v1/products/10, GET /loja/v2/products/99
/reports/{date}                      → GET /loja/reports/2024-01-15
/search?q=[string]&page=[int]        → GET /loja/search?q=hello&page=2
/users/{uuid?}                       → GET /loja/users  ou  GET /loja/users/3fa85f64-...
/                                    → GET /loja
```

---

## Variáveis no response body

O response body suporta substituição de variáveis com a sintaxe `{{expressão}}`. Os valores são resolvidos a cada requisição.

| Expressão | Resultado |
| --- | --- |
| `{{uuid}}` | UUID gerado aleatoriamente |
| `{{$.propriedade}}` | Valor do campo `propriedade` do request body (JSON) |
| `{{$.user.address.city}}` | Suporta caminhos aninhados |
| `{{$.items[0]}}` | Suporta acesso a índices de array |

**Regras:**

- Se a propriedade JSONPath não existir no body → substituída por string vazia
- Se o request body estiver ausente ou não for JSON válido → todas as expressões `{{$.x}}` viram string vazia
- Expressões não reconhecidas → mantidas literalmente na saída

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

Resposta retornada:

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

Todos os exemplos assumem uma aplicação com slug `loja`. Crie cada mock no Gerenciador de Mocks com os valores indicados.

### Simular criação de recurso com ID dinâmico

| Campo | Valor |
| --- | --- |
| Método | `POST` |
| Pattern | `/api/users` |
| Status Code | `201` |
| Response body | `{ "id": "{{uuid}}", "name": "{{$.name}}", "email": "{{$.email}}" }` |

```bash
curl -X POST http://localhost:5087/loja/api/users \
  -H "Content-Type: application/json" \
  -d '{ "name": "Alice", "email": "alice@example.com" }'
```

```json
{ "id": "a3f2c1d4-...", "name": "Alice", "email": "alice@example.com" }
```

### Simular timeout / endpoint lento

| Campo | Valor |
| --- | --- |
| Método | `GET` |
| Pattern | `/api/reports` |
| Status Code | `504` |
| Processing time | `60000` |
| Response body | `{ "error": "Gateway Timeout" }` |

### Simular erro de autenticação

| Campo | Valor |
| --- | --- |
| Método | `GET` |
| Pattern | `/api/protected` |
| Status Code | `401` |
| Response body | `{ "error": "Unauthorized" }` |

### Endpoint com parâmetro tipado na URL

| Campo | Valor |
| --- | --- |
| Método | `GET` |
| Pattern | `/api/orders/{guid}` |
| Response body | `{ "id": "{{uuid}}", "status": "shipped" }` |

Aceita: `GET /loja/api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6`

Rejeita com 404: `GET /loja/api/orders/abc` (não é um UUID válido)

### Endpoint com query string tipada

| Campo | Valor |
| --- | --- |
| Método | `GET` |
| Pattern | `/api/products?category=[string]&page=[int]` |
| Response body | `{ "items": [] }` |

Aceita: `GET /loja/api/products?category=electronics&page=1`

Rejeita com 404: `GET /loja/api/products?category=electronics&page=abc` (page não é inteiro)

### Endpoint com versão curinga

| Campo | Valor |
| --- | --- |
| Método | `GET` |
| Pattern | `/v*/health` |
| Response body | `{ "status": "ok" }` |

Aceita: `/loja/v1/health`, `/loja/v2/health`, `/loja/v10/health`

---

## Rotas reservadas

Os primeiros segmentos de path abaixo pertencem ao sistema. Eles nunca chegam ao despachante de mocks e **não podem ser usados como slug de aplicação**:

| Segmento | Uso |
| --- | --- |
| `/account` | Login, cadastro e logout |
| `/apps` | Páginas do Gerenciador de Mocks |
| `/signin-github` | Callback do OAuth do GitHub |
| `/_blazor` | Hub SignalR do Blazor |
| `/_framework` | Arquivos de framework Blazor |
| `/_content` | Conteúdo estático de componentes |
| `/css` | Folhas de estilo |
| `/js` | Scripts |
| `/favicon.ico` | Ícone do site |

---

## Persistência

Usuários, aplicações e mocks são gravados em um banco SQLite em `RestMock/mocks.db`, criado automaticamente na inicialização. Excluir uma aplicação exclui seus mocks. Para zerar tudo, pare o servidor e apague o arquivo.

---

## Tecnologias

- .NET 10 / ASP.NET Core
- Blazor Server (interface visual) e Razor Pages (páginas de conta)
- Autenticação por cookie + OAuth do GitHub ([AspNet.Security.OAuth.GitHub](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers))
- SQLite via Microsoft.Data.Sqlite
- Newtonsoft.Json (JSONPath no templating de resposta)
- YamlDotNet (exportação OpenAPI)
