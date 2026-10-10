# CLAUDE.md — RestMock

Guia de referência para desenvolvimento e manutenção do RestMock.

## Visão geral

RestMock é um servidor HTTP de mocks multiusuário com persistência em SQLite. Cada **usuário** tem N **aplicações**, e cada aplicação tem N **mocks** (endpoints). O isolamento é total: um usuário só vê e altera as próprias aplicações.

Pontos de entrada:

- **Chamadas mockadas** — `/{slug-da-aplicação}/{pattern}`, anônimas, despachadas pelo `RewritterMiddleware`
- **Páginas de conta** (Razor Pages) — `/account/login`, `/account/register`, `/account/logout`
- **UI de gerenciamento** (Blazor Server, exige login) — `/` (lista de aplicações) e `/apps/{applicationId}` (Gerenciador de Mocks)

Não existe API REST de gerenciamento; mocks são gerenciados apenas pela UI.

---

## Arquitetura

```text
Requisição HTTP
    │
    ▼
UseStaticFiles               ← /css, /js, ...
    │
    ▼
RewritterMiddleware          ← antes de autenticação e roteamento
    ├── 1º segmento vazio ou em ReservedRoutes? → _next()
    ├── ApplicationRepository.GetBySlug(slug) == null? → _next()
    └── MockRepository.GetByApplication(app.Id).First(Match(resto do path))
            ├── Achou → ResponseTemplateProcessor.Process() → resposta
            └── Não achou → 404 (não cai na UI)

Autenticação (cookie + GitHub OAuth opcional) → Roteamento
    ├── Razor Pages  /account/*           ← login, cadastro, logout (anônimas)
    ├── Blazor hub   /_blazor             ← RequireAuthorization
    └── _Host        /, /apps/{id}, ...   ← [Authorize]; anônimo → redirect /account/login
```

O banco é a fonte de verdade em runtime: o middleware consulta o SQLite a cada chamada mockada (não há cache em memória).

### Camadas

| Camada | Pasta | Responsabilidade |
| --- | --- | --- |
| Domain | `Domain/` | Modelos, rotas reservadas, matching, processamento de templates |
| Middleware | `Middlewares/` | Despacho das requisições mockadas por slug |
| Repositories | `Repositories/` | SQL (ADO.NET puro) sobre `mocks.db` |
| Services | `Services/` | Regras de negócio e validação usadas pela UI (contas, aplicações, mocks) |
| Pages | `Pages/` | Blazor (`*.razor`) e Razor Pages de conta (`Pages/Account/*.cshtml`) |

---

## Arquivos-chave

| Arquivo | Papel |
| --- | --- |
| `Domain/UserModel.cs` | Usuário (senha e/ou GitHub) |
| `Domain/ApplicationModel.cs` | Aplicação: dono, nome, slug |
| `Domain/EndpointModel.cs` | Mock + `ApplicationId` + lógica de pattern matching |
| `Domain/ReservedRoutes.cs` | Primeiros segmentos do sistema (não despachados, não usáveis como slug) |
| `Domain/ResponseTemplateProcessor.cs` | Substitui `{{uuid}}` e `{{$.path}}` no response body |
| `Middlewares/RewritterMiddleware.cs` | Separa slug do path e despacha o mock da aplicação |
| `Repositories/SqliteDatabase.cs` | Conexão + schema (`CREATE TABLE IF NOT EXISTS`) + helpers |
| `Repositories/UserRepository.cs` / `ApplicationRepository.cs` / `MockRepository.cs` | CRUD de cada tabela |
| `Services/UserService.cs` | Cadastro, validação de senha (`PasswordHasher`), usuário GitHub |
| `Services/ApplicationService.cs` | Criação com slug, posse (`GetOwned`), exclusão |
| `Services/MockClientService.cs` | CRUD de mocks escopado por aplicação; evento `OnChange(applicationId)` |
| `Services/AccountClaims.cs` | Monta o `ClaimsPrincipal` e lê o id do usuário (`GetUserId()`) |
| `Pages/Account/*` | Login, cadastro, logout (Razor Pages, emitem/limpam o cookie) |
| `Pages/Applications.razor` | Lista e criação de aplicações (`/`) |
| `Pages/Client.razor` | Gerenciador de Mocks de uma aplicação (`/apps/{id}`) |
| `Program.cs` | DI, autenticação (cookie + GitHub), pipeline |

---

## Regras de negócio importantes

### Isolamento por usuário

- Toda tela Blazor obtém o usuário via `[CascadingParameter] Task<AuthenticationState>` + `User.GetUserId()`
- Acesso a uma aplicação sempre passa por `ApplicationService.GetOwned(appId, userId)` — retorna `null` tanto para inexistente quanto para de outro usuário (não revela existência)
- `MockClientService.Update/Remove` recebem o `applicationId` e lançam `ArgumentException` se o mock não pertence àquela aplicação
- O cookie é revalidado em `OnValidatePrincipal`: se o usuário não existe mais no banco (ex.: `mocks.db` apagado), a sessão é rejeitada

### Autenticação

- Cookie `RestMock.Auth`, 14 dias, sliding. Claims: `NameIdentifier` = `UserModel.Id`, `Name` = `DisplayName`
- Login/cadastro/logout são **Razor Pages** porque cookies só podem ser emitidos numa resposta HTTP, não pelo circuito SignalR do Blazor
- Logout é `POST /account/logout` com `[IgnoreAntiforgeryToken]` (o botão fica num componente Blazor, que não emite token de antiforgery do Razor Pages)
- GitHub só é registrado se `Authentication:GitHub:ClientId` e `ClientSecret` estiverem configurados (user-secrets ou variáveis de ambiente — nunca no `appsettings.json`). Em `OnCreatingTicket`, os claims do GitHub são trocados pelos do usuário RestMock (`FindOrCreateGitHubUser`)
- Usuário com senha e usuário GitHub são contas distintas (não há vinculação)
- `ReturnUrl` só é aceito se for local (`Url.IsLocalUrl`)

### Slugs e rotas reservadas

Slug: `^[a-z0-9]+(?:-[a-z0-9]+)*$`, até 50 caracteres, único globalmente (case-insensitive), gerado por `ApplicationService.Slugify(nome)` quando vazio.

`ReservedRoutes.Segments` lista os primeiros segmentos do sistema:

```text
account  apps  signin-github  _blazor  _framework  _content  css  js  favicon.ico
```

Ao adicionar uma nova rota de sistema (página, endpoint, callback), inclua o primeiro segmento nessa lista — senão um slug igual sequestra a rota.

### Pattern matching

O middleware remove `/{slug}` do path (path vazio vira `/`) e chama `EndpointModel.Match()` com `http://localhost{resto}{query}`. O matching é sempre **case-insensitive**. Ordem de avaliação:

1. Método HTTP deve ser igual
2. Path deve corresponder ao regex gerado por `BuildPathRegex()`
3. Se o pattern tem query string, cada parâmetro declarado deve estar presente com o tipo correto

Entre os mocks da aplicação, vence o primeiro na ordem da coluna `Position` (ordem de criação).

Tipos suportados no path: `uuid`/`guid`, `int`/`long`/`number`, `date`, `datetime`, qualquer outro nome → `[^/]+`.
Tipos suportados na query string: `[guid]`, `[int]`, `[date]`/`[datetime]`, `[string]`.

Para adicionar um novo tipo de path, edite `EndpointModel.TypeToRegex()`.

### Response templating

Processado em `ResponseTemplateProcessor.Process()` via regex `\{\{([^}]+)\}\}`.

Variáveis suportadas:

- `{{uuid}}` → `Guid.NewGuid().ToString()`
- `{{$.caminho}}` → `JToken.SelectToken(expression)` no request body (JSONPath via Newtonsoft.Json)

Para adicionar uma nova variável, insira um novo `if` no lambda de substituição em `ResponseTemplateProcessor.Process()` antes do bloco `{{$.`.

Comportamento quando a propriedade JSONPath não existe: retorna `string.Empty` (nunca lança exceção).

### Persistência

`SqliteDatabase` (Microsoft.Data.Sqlite, ADO.NET puro) abre `mocks.db` no `ContentRootPath` e cria o schema no construtor:

```text
Users        (Id, DisplayName, UserName UNIQUE NOCASE NULL, PasswordHash NULL, GitHubId UNIQUE NULL, CreatedAt)
Applications (Id, UserId → Users ON DELETE CASCADE, Name, Slug UNIQUE NOCASE, Description NULL, CreatedAt)
Mocks        (Id, ApplicationId → Applications ON DELETE CASCADE, Position, HttpMethod, Pattern, StatusCode,
              ProcessingTime, ContentType, Description NULL, ResponseBody NULL)
```

- `Foreign Keys=True` na connection string — exclusões em cascata dependem disso
- Pooling desabilitado para liberar o arquivo após cada operação (testes apagam o arquivo no Windows)
- Datas gravadas como texto ISO 8601 UTC (`ToDbValue()` / `GetUtcDateTime()`)
- `ResponseBody` é gravado como JSON (Newtonsoft) — uma `string` volta como `string` e um objeto aninhado volta como `JObject`/`JArray`
- `Position` de um mock novo = `MAX(Position) + 1` dentro da aplicação; `Update` preserva `Position` e `ApplicationId`
- Use o helper `Parameters.AddValue(nome, valor)` (converte `null` em `DBNull`)

Repositórios e serviços são singletons sem estado; cada operação abre e fecha sua conexão.

Mudança de schema: `CREATE TABLE IF NOT EXISTS` não altera tabelas existentes — adicione o `ALTER TABLE` correspondente em `SqliteDatabase.EnsureSchema()`.

### ResponseBody

O campo `ResponseBody` é `object?`. Pode conter:

- `string` (quando enviado pelo formulário Blazor)
- `JObject` / `JArray` (JSON aninhado gravado no banco)
- `null` (resposta sem body)

Sempre use `.ToString()` para serializar para a resposta HTTP. Nunca assuma o tipo concreto.

### Description

Campo `string?` opcional em `EndpointModel` e `ApplicationModel`. Puramente informativo — não afeta matching nem templating.

---

## Convenções de código

- Sem comentários óbvios; apenas onde o **porquê** não é evidente
- Sem abstrações preventivas: classes e interfaces surgem quando há segunda implementação real (repositórios e serviços são classes concretas)
- Validações apenas nas bordas (serviços chamados pela UI e Razor Pages); repositórios confiam nos invariantes
- Serviços retornam `(Resultado?, string? Error)` para erros de validação esperados; exceções só para violações de posse/estado
- Mensagens para o usuário em português
- `ProcessingTime` é sempre `>= 0` — o setter garante isso com `Math.Max`
- Não use `--no-verify` nem `--force-push` sem instrução explícita do usuário

---

## Como adicionar funcionalidades

### Nova variável de template (`{{foo}}`)

1. Em `ResponseTemplateProcessor.cs`, adicione um `if` no lambda:

   ```csharp
   if (expression.Equals("foo", StringComparison.OrdinalIgnoreCase))
       return /* valor */;
   ```

2. Atualize o tooltip em `Client.razor` (`<ul>` dentro do `.tooltip` do Response body)
3. Documente nos READMEs (`README.md` e `README.pt-BR.md`) na tabela de variáveis

### Novo tipo de path (`{tipo}`)

1. Em `EndpointModel.TypeToRegex()`, adicione um `case`:

   ```csharp
   "foo" => @"seu-regex-aqui",
   ```

2. Atualize o tooltip em `Client.razor` (campo Pattern)
3. Documente nos READMEs

### Novo campo em um modelo

1. Adicione a propriedade no modelo (`Domain/`)
2. Adicione a coluna em `SqliteDatabase.EnsureSchema()` (`CREATE TABLE` + `ALTER TABLE` para bancos existentes)
3. Inclua a coluna no `INSERT`/`UPDATE`/`SELECT` e no `Read()` do repositório
4. Adicione o campo no formulário (`Client.razor` ou `Applications.razor`)

### Nova página ou rota de sistema

1. Adicione o primeiro segmento em `ReservedRoutes.Segments`
2. Páginas Blazor já exigem login (via `_Host`); Razor Pages anônimas precisam de `[AllowAnonymous]` ou herdar de `AccountPageModel`

---

## Desenvolvimento

```bash
# rodar localmente (abre o navegador automaticamente)
run.bat

# build sem servidor rodando
dotnet build RestMock

# o servidor deve estar parado para o build sobrescrever o executável
```

### Portas padrão

| Ambiente | URL |
| --- | --- |
| HTTP | `http://localhost:5087` |
| HTTPS | `https://localhost:7253` |

### Login com GitHub em desenvolvimento

```bash
dotnet user-secrets --project RestMock set "Authentication:GitHub:ClientId" "<id>"
dotnet user-secrets --project RestMock set "Authentication:GitHub:ClientSecret" "<secret>"
```

Callback do OAuth App: `http://localhost:5087/signin-github`.

### Banco de dados

`RestMock/mocks.db` — criado automaticamente na inicialização. Para zerar usuários, aplicações e mocks, pare o servidor e delete o arquivo. Não versionar (está no `.gitignore`).

---

## Testes unitários

O projeto de testes fica em `RestMock.Tests/` e usa xUnit + FluentAssertions. Para executar:

```bash
dotnet test RestMock.Tests
```

**Regra obrigatória:** toda modificação de código deve vir acompanhada de testes. A cobertura deve ser mantida ou ampliada — nunca reduzida.

- **Nova lógica** → novos testes cobrindo o caminho feliz e os casos de borda
- **Bug corrigido** → teste que reproduz o bug antes da correção (e passa depois)
- **Comportamento alterado** → testes existentes atualizados para refletir o novo contrato
- **Código removido** → testes correspondentes também removidos

### Onde cada classe é testada

| Classe | Arquivo de testes |
| --- | --- |
| `EndpointModel` | `Domain/EndpointModelTests.cs` |
| `ResponseTemplateProcessor` | `Domain/ResponseTemplateProcessorTests.cs` |
| `SqliteDatabase` | `Repositories/SqliteDatabaseTests.cs` |
| `UserRepository` | `Repositories/UserRepositoryTests.cs` |
| `ApplicationRepository` | `Repositories/ApplicationRepositoryTests.cs` |
| `MockRepository` | `Repositories/MockRepositoryTests.cs` |
| `UserService` | `Services/UserServiceTests.cs` |
| `ApplicationService` | `Services/ApplicationServiceTests.cs` |
| `MockClientService` | `Services/MockClientServiceTests.cs` |
| `OpenApiExporter` | `Services/OpenApiExporterTests.cs` |
| `RewritterMiddleware` | `Middlewares/RewritterMiddlewareTests.cs` |

### Detalhes de infraestrutura

- `TestDatabase` cria um SQLite temporário por instância da classe de teste (apaga no `Dispose`) e expõe os três repositórios + helpers `AddUser()` / `AddApplication()`
- Serviços e middleware são testados contra o SQLite real (sem mocks de repositório)
- Não há estado estático compartilhado: os testes rodam em paralelo
- O middleware é testado com `DefaultHttpContext` chamando `InvokeAsync(context, applications, mocks)` diretamente

---

## O que NÃO fazer

- Não acessar aplicação ou mock pelo id sem checar posse (`ApplicationService.GetOwned` / `applicationId` no `MockClientService`)
- Não emitir/limpar o cookie de autenticação dentro de componentes Blazor — use Razor Pages
- Não colocar `ClientSecret` do GitHub em arquivos versionados
- Não criar rota de sistema sem registrar o segmento em `ReservedRoutes`
- Não assumir que `ResponseBody` é `string` — pode ser `JObject` ou outro tipo após desserialização
- Não trocar o SQLite por outro banco (ou adicionar ORM) sem discutir primeiro — o escopo do projeto é propositalmente simples (arquivo local, ADO.NET puro)
- Não reintroduzir uma API REST de gerenciamento sem discutir autenticação dela (cookie da UI não serve para scripts/CI). Se voltar a existir, mantenha um `app.http` na raiz com exemplos de todas as chamadas, atualizado na mesma entrega de qualquer mudança de contrato
