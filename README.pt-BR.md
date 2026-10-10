# imockey

[![.NET](https://github.com/phduarte/imockey/actions/workflows/dotnet.yml/badge.svg)](https://github.com/phduarte/imockey/actions/workflows/dotnet.yml)
[![Container](https://github.com/phduarte/imockey/actions/workflows/container.yml/badge.svg)](https://github.com/phduarte/imockey/actions/workflows/container.yml)

Servidor HTTP multiusuário para criação de endpoints mockados. Útil em testes de integração, testes de estresse, simulação de caos, debugging e desenvolvimento desacoplado.

> Também disponível em [English](README.md).

## Sumário

- [Conceitos](#conceitos)
- [Início rápido](#início-rápido)
- [Contas e login](#contas-e-login)
- [Aplicações](#aplicações)
- [Gerenciador de Rotas](#gerenciador-de-rotas)
- [Chamando uma rota](#chamando-uma-rota)
- [Regras de pattern matching](#regras-de-pattern-matching)
- [Variáveis no response body](#variáveis-no-response-body)
- [Casos de uso](#casos-de-uso)
- [Rotas reservadas](#rotas-reservadas)
- [Persistência](#persistência)
- [Implantação](#implantação)
- [Desenvolvimento](#desenvolvimento)
- [Análise de mercado](#análise-de-mercado)
- [Tecnologias](#tecnologias)

---

## Conceitos

**Usuário** — Quem faz login no imockey. Cada usuário só vê e gerencia as próprias aplicações.

**Aplicação** — Um conjunto de rotas que simula uma API real. Um usuário pode ter quantas aplicações quiser. Cada uma tem um **slug** que, precedido do nome do usuário dono (`{usuário}-{slug}`, ex.: `alice-loja`), vira o primeiro segmento da URL das suas rotas.

**Rota** — Cópia não funcional de um endpoint real. Retorna respostas configuradas sem nenhuma lógica de negócio. Toda rota pertence a uma aplicação.

**Pattern** — Endereço do endpoint mockado dentro da aplicação (sem host, usuário nem slug), podendo conter curingas e tipos de validação.

**Processing time** — Atraso artificial aplicado antes de responder, útil para simular latência ou timeout.

As rotas são gerenciadas apenas pela interface web; não há API REST de gerenciamento.

---

## Início rápido

Requer o [SDK do .NET 10](https://dotnet.microsoft.com/download).

```bash
# clonar e executar
git clone https://github.com/phduarte/imockey
cd imockey
run.bat                          # Windows
dotnet run --project Imockey    # qualquer plataforma
```

O `run.bat` libera as portas 5087 e 7253 se estiverem em uso, inicia o servidor e abre o navegador em `http://localhost:5087`.

1. Crie uma conta em `/account/register`, por exemplo com o usuário `alice` (ou entre com GitHub, se configurado).
2. Crie uma aplicação, por exemplo **Loja API** com slug `loja`. As rotas dela ficarão em `/alice-loja`.
3. No Gerenciador de Rotas da aplicação, crie uma rota, por exemplo `GET /v1/products`.
4. Chame a rota: `curl http://localhost:5087/alice-loja/v1/products`.

---

## Contas e login

| Endereço | Descrição |
| --- | --- |
| `/account/login` | Login com usuário e senha, ou com GitHub |
| `/account/register` | Cadastro com usuário, nome de exibição opcional e senha |
| `/account/logout` | Encerra a sessão (botão **Sair** na barra superior) |

Todas as páginas da interface exigem usuário logado; visitantes anônimos são redirecionados para o login. As chamadas às rotas **não** exigem login.

### Usuário e senha

- Usuário: de 3 a 32 caracteres — letras, números e `-` (sem `-` no início, no fim ou repetido). Ele faz parte da URL das rotas, então caracteres que não podem ser usados em URL são rejeitados. É gravado em minúsculas e deve ser único.
- Nome de exibição: opcional; aparece na barra superior. Se vazio, usa o nome de usuário.
- Senha: no mínimo 8 caracteres, armazenada como hash com salt (`PasswordHasher` do ASP.NET Core Identity).
- O cookie de sessão dura 14 dias (renovado com o uso). Se o usuário não existir mais no banco, a sessão é rejeitada e é preciso entrar de novo.

### GitHub

O botão **Entrar com GitHub** só aparece quando há um OAuth App configurado:

1. Crie um OAuth App em <https://github.com/settings/developers> com a callback URL `http://localhost:5087/signin-github` (ajuste host/porta).
2. Guarde as credenciais fora do repositório, por exemplo com user-secrets:

   ```bash
   dotnet user-secrets --project Imockey init
   dotnet user-secrets --project Imockey set "Authentication:GitHub:ClientId" "<client id>"
   dotnet user-secrets --project Imockey set "Authentication:GitHub:ClientSecret" "<client secret>"
   ```

   Ou use as variáveis de ambiente `Authentication__GitHub__ClientId` e `Authentication__GitHub__ClientSecret`.

O primeiro login com GitHub cria um usuário do imockey vinculado àquela conta do GitHub, com nome de usuário derivado do login do GitHub (em minúsculas, com caracteres não seguros em URL trocados por `-`; se já estiver em uso, recebe um sufixo numérico como `-2`). Usuários do GitHub e usuários com senha são contas separadas — nunca são vinculadas.

---

## Aplicações

Depois do login, a página inicial (`/`) lista suas aplicações e tem um formulário para criar uma nova.

| Campo | Obrigatório | Descrição |
| --- | --- | --- |
| Nome | sim | Nome de exibição |
| Slug | não | Letras minúsculas, números e `-`, até 50 caracteres. Gerado a partir do nome quando vazio (`Serviço de Pagamentos` → `servico-de-pagamentos`). O formulário mostra a URL base resultante. |
| Descrição | não | Texto livre |

O slug gravado é sempre precedido do seu nome de usuário: o slug `servico-de-pagamentos` do usuário `alice` vira `alice-servico-de-pagamentos`, o primeiro segmento da URL das rotas. Ele deve ser único entre todos os usuários e não pode ser uma [rota reservada](#rotas-reservadas).

Depois de criar a aplicação, você é levado ao Gerenciador de Rotas dela. A lista mostra a URL base, a quantidade de rotas e a data de criação de cada aplicação. Clique no nome (ou em **→**) para abrir o Gerenciador de Rotas. **✕** exclui a aplicação **e todas as suas rotas** (pede confirmação antes).

---

## Gerenciador de Rotas

Aberto em `/apps/{applicationId}`. Só o dono consegue abrir; qualquer outro usuário vê "Aplicação não encontrada", exista ela ou não.

O cabeçalho mostra a URL base da aplicação (`http://localhost:5087/{usuário}-{slug}/...`). A tela é dividida em duas colunas:

- **Esquerda** — formulário de criação e edição de rotas
- **Direita** — lista das rotas da aplicação

A lista é atualizada automaticamente em tempo real via Blazor Server (SignalR) quando as rotas da aplicação mudam.

### Formulário

| Campo | Tipo | Padrão | Descrição |
| --- | --- | --- | --- |
| Descrição | texto | — | Texto livre para documentar o propósito da rota. Opcional. |
| Método HTTP | dropdown | GET | GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS |
| Pattern (URL) | texto | — | Endereço do endpoint dentro da aplicação. Obrigatório; a `/` inicial é adicionada se faltar. Suporta curingas (ver [Regras de pattern matching](#regras-de-pattern-matching)) |
| Status Code | número | 200 | Código HTTP de resposta, entre 100 e 599. Sugere os códigos mais comuns. |
| Processing time (ms) | número | 0 | Atraso em milissegundos antes de responder (valores negativos viram 0) |
| Content-Type | dropdown | application/json | Tipo MIME da resposta (JSON, XML, texto, HTML, CSV, imagens etc.) |
| Response body | textarea | — | Corpo da resposta. Opcional. Suporta variáveis (ver [Variáveis no response body](#variáveis-no-response-body)) |

**Botões:** **Criar rota** / **Salvar alterações**, **{ } Formatar** (formata o JSON do response body), **Limpar** / **Cancelar**.

### Lista de rotas

| Botão | Ação |
| --- | --- |
| **▸** | Expande os detalhes da rota (ID, descrição e response body) |
| **✎** | Carrega a rota no formulário para edição |
| **⏸** / **▶** | Desativa / reativa a rota. Desativada, ela aparece esmaecida com a marca "desativada" e é ignorada nas chamadas: a requisição cai na próxima rota que casar ou recebe 404. Editar uma rota desativada não a reativa. |
| **<>** | Copia o comando `curl` equivalente (com a URL completa) para a área de transferência |
| **✕** | Exclui a rota (pede confirmação antes) |

No cabeçalho da lista:

- **Exportar YAML** baixa `{usuário}-{slug}.openapi.yaml`, um documento OpenAPI 3 com as rotas da aplicação, com o nome da aplicação como título e a URL base em `servers`. Fica desabilitado enquanto a aplicação não tem rotas.
- **Atualizar** recarrega a lista.

---

## Chamando uma rota

```text
{método} http://localhost:5087/{usuário}-{slug}{pattern}
```

| Requisição | Resultado |
| --- | --- |
| Primeiro segmento e pattern casam com uma rota | Status, content type e body da rota, depois do processing time |
| Primeiro segmento pertence a uma aplicação, mas nenhuma rota casa | `404 Not Found` |
| Primeiro segmento não pertence a nenhuma aplicação | Segue para a interface (redirect para o login ou "Página não encontrada") |

Rotas de uma aplicação nunca são encontradas pela URL de outra. O primeiro segmento não diferencia maiúsculas de minúsculas.

---

## Regras de pattern matching

Depois de remover o segmento `/{usuário}-{slug}` do path (path vazio vira `/`), o matching verifica, nesta ordem:

1. **Método HTTP** — deve ser igual (case-insensitive)
2. **Path** — deve corresponder ao pattern (case-insensitive)
3. **Query string** — se o pattern declarar query, todos os parâmetros declarados devem estar presentes na requisição, com o tipo correto quando houver tipo declarado

Parâmetros extras na query string da requisição são ignorados. Quando mais de uma rota casa, vence a criada primeiro.

### Curingas no path

| Sintaxe | Corresponde a | Exemplo de pattern | Exemplo de URL |
| --- | --- | --- | --- |
| `*` | Qualquer valor em um segmento | `/v*/users` | `/v1/users`, `/v2/users` |
| `{guid}` ou `{uuid}` | UUID/GUID | `/users/{guid}` | `/users/3fa85f64-...` |
| `{int}`, `{long}`, `{number}` | Número inteiro (positivo ou negativo) | `/items/{int}` | `/items/42`, `/items/-1` |
| `{date}` | Data no formato `YYYY-MM-DD` | `/events/{date}` | `/events/2024-12-31` |
| `{datetime}` | Data/hora ISO 8601 | `/logs/{datetime}` | `/logs/2024-12-31T10:00:00Z` |
| `{nome}` | Qualquer segmento não vazio (qualquer outro nome) | `/users/{id}` | `/users/abc`, `/users/123` |
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
| `[string]` | Qualquer valor (só a presença é verificada) |

Um valor literal (ex.: `?status=active`) não é comparado — só a presença do parâmetro é verificada.

**Exemplos** (aplicação `loja` do usuário `alice`):

```text
/api/orders                          → GET /alice-loja/api/orders
/api/orders/{guid}                   → GET /alice-loja/api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6
/v*/products/{int}                   → GET /alice-loja/v1/products/10, GET /alice-loja/v2/products/99
/reports/{date}                      → GET /alice-loja/reports/2024-01-15
/search?q=[string]&page=[int]        → GET /alice-loja/search?q=hello&page=2
/users/{uuid?}                       → GET /alice-loja/users  ou  GET /alice-loja/users/3fa85f64-...
/                                    → GET /alice-loja
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
- Se o request body estiver ausente ou não for JSON válido → as expressões `{{$.x}}` são mantidas literalmente na saída
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

Todos os exemplos assumem o usuário `alice` com uma aplicação de slug `loja` (URL base `/alice-loja`). Crie cada rota no Gerenciador de Rotas com os valores indicados.

### Simular criação de recurso com ID dinâmico

| Campo | Valor |
| --- | --- |
| Método | `POST` |
| Pattern | `/api/users` |
| Status Code | `201` |
| Response body | `{ "id": "{{uuid}}", "name": "{{$.name}}", "email": "{{$.email}}" }` |

```bash
curl -X POST http://localhost:5087/alice-loja/api/users \
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

Aceita: `GET /alice-loja/api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6`

Rejeita com 404: `GET /alice-loja/api/orders/abc` (não é um UUID válido)

### Endpoint com query string tipada

| Campo | Valor |
| --- | --- |
| Método | `GET` |
| Pattern | `/api/products?category=[string]&page=[int]` |
| Response body | `{ "items": [] }` |

Aceita: `GET /alice-loja/api/products?category=electronics&page=1`

Rejeita com 404: `GET /alice-loja/api/products?category=electronics&page=abc` (page não é inteiro)

### Endpoint com versão curinga

| Campo | Valor |
| --- | --- |
| Método | `GET` |
| Pattern | `/v*/health` |
| Response body | `{ "status": "ok" }` |

Aceita: `/alice-loja/v1/health`, `/alice-loja/v2/health`, `/alice-loja/v10/health`

---

## Rotas reservadas

Os primeiros segmentos de path abaixo pertencem ao sistema. Eles nunca chegam ao despachante de rotas, e uma aplicação cujo `{usuário}-{slug}` seja igual a um deles é recusada:

| Segmento | Uso |
| --- | --- |
| `/account` | Login, cadastro e logout |
| `/apps` | Páginas do Gerenciador de Rotas |
| `/signin-github` | Callback do OAuth do GitHub |
| `/_blazor` | Hub SignalR do Blazor |
| `/_framework` | Arquivos de framework Blazor |
| `/_content` | Conteúdo estático de componentes |
| `/css` | Folhas de estilo |
| `/js` | Scripts |
| `/favicon.ico` | Ícone do site |

---

## Persistência

Usuários, aplicações, rotas e as chaves de Data Protection do ASP.NET Core (que assinam o cookie de login) são gravados em um banco SQLite em `Imockey/data/mocks.db`, criado automaticamente na inicialização. Excluir uma aplicação exclui suas rotas.

O local pode ser alterado pela configuração `Database:Path` (variável de ambiente `Database__Path`); caminhos relativos são resolvidos a partir da pasta da aplicação. O banco roda em modo WAL, por isso podem aparecer os arquivos `mocks.db-wal` e `mocks.db-shm` ao lado dele.

Para zerar tudo, pare o servidor e apague o `mocks.db` (e o `mocks.db-wal` / `mocks.db-shm`, se existirem). As sessões existentes deixam de valer e os usuários precisam se cadastrar de novo.

---

## Implantação

O repositório inclui um `Dockerfile` que roda o imockey sob o [Litestream](https://litestream.io), que replica o `mocks.db` para o Azure Blob Storage e o restaura a cada subida do container. Isso permite rodar em plataformas que escalam para zero.

- A imagem escuta na porta `8080` e guarda o banco em `/data/mocks.db`.
- A replicação é ativada quando `AZURE_STORAGE_ACCOUNT` e `LITESTREAM_AZURE_ACCOUNT_KEY` estão definidas; sem elas, o app roda sem replicação.
- Rode **no máximo uma réplica**: SQLite + Litestream assumem um único processo gravando.
- O workflow `Container` publica a imagem no GitHub Container Registry (`ghcr.io/phduarte/imockey`, tags `latest` e o SHA curto do commit) depois que o workflow `.NET` passa no `master`.

O passo a passo para o Azure Container Apps, com foco em custo próximo de zero, está em [docs/deploy-azure-container-apps.md](docs/deploy-azure-container-apps.md).

Para rodar a imagem localmente, sem replicação, usando a mesma pasta `Imockey/data` do desenvolvimento local (execute na raiz do repositório):

```bash
docker build -t imockey .
docker run --rm -p 8080:8080 -v "$PWD/Imockey/data:/data" imockey
```

No PowerShell do Windows, use `-v "${PWD}\Imockey\data:/data"`. Monte a **pasta**, não o arquivo `mocks.db` (o SQLite em modo WAL grava `mocks.db-wal`/`mocks.db-shm` ao lado dele). Não rode o container e o `dotnet run` sobre a mesma pasta ao mesmo tempo. Sem `-v`, os dados se perdem quando o container é removido.

---

## Desenvolvimento

| Ambiente | URL |
| --- | --- |
| HTTP | `http://localhost:5087` |
| HTTPS | `https://localhost:7253` |

```bash
dotnet build Imockey        # pare o servidor antes, para o build sobrescrever o executável
dotnet test Imockey.Tests   # xUnit + FluentAssertions, contra um SQLite temporário
```

Orientações para contribuir (arquitetura, regras de negócio e como adicionar funcionalidades) estão no [CLAUDE.md](CLAUDE.md).

---

## Análise de mercado

Uma análise de funcionalidades, um benchmark com produtos semelhantes e sugestões de melhorias para um plano pago estão em [docs/analise-de-mercado.md](docs/analise-de-mercado.md).

---

## Tecnologias

- .NET 10 / ASP.NET Core
- Blazor Server (interface de gerenciamento) e Razor Pages (páginas de conta)
- Autenticação por cookie + OAuth do GitHub ([AspNet.Security.OAuth.GitHub](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers))
- SQLite via Microsoft.Data.Sqlite (ADO.NET puro)
- Newtonsoft.Json (JSONPath no templating de resposta)
- YamlDotNet (exportação OpenAPI)
- Docker + Litestream (replicação para o Azure Blob Storage)
- xUnit + FluentAssertions (testes)
