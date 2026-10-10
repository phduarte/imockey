# Análise do imockey: funcionalidades, benchmark e diferenciais para um plano pago

## 1. O que o imockey faz hoje

| Área | Funcionalidade atual |
| --- | --- |
| Organização | Usuário → aplicações (slug global) → mocks. Isolamento total entre usuários |
| Matching | Método + path com tipos (`{guid}`, `{int}`, `{date}`, `{datetime}`, `*`, opcionais `{x?}`) + validação de tipos na query string (`[int]`, `[guid]`…) |
| Resposta | Status, content-type, body estático, atraso fixo (`ProcessingTime`) |
| Templating | Só `{{uuid}}` e `{{$.jsonpath}}` sobre o body da requisição |
| UI | Blazor em tempo real, formatar JSON, copiar `curl`, exportar OpenAPI 3 (YAML) |
| Contas | Usuário/senha e GitHub OAuth |
| Infra | SQLite + Litestream, 1 réplica no Azure Container Apps, escala até zero |

**Pontos fortes:** é simples, a interface está em português, o setup é rápido, o custo de infra é baixo e a tipagem no matching é boa. Muitos concorrentes não validam tipo na query string.

**Lacunas em relação ao mercado:**
- Não dá para configurar **headers de resposta** além do content-type.
- Não há **histórico das requisições** recebidas.
- Cada mock tem **uma única resposta**, sem condições nem variações.
- O templating é pobre: não captura parâmetros do path, nem headers, nem gera dados fake.
- Não importa OpenAPI nem Postman (só exporta).
- Não há API nem CLI, então não serve para CI.
- Não há times nem compartilhamento.

## 2. Benchmark

Os preços são aproximados e de memória (sem pesquisa web nesta análise). Confira antes de usar em uma decisão de preço.

| Produto | Modelo | Destaques | Preço (aprox.) |
| --- | --- | --- | --- |
| **Beeceptor** | SaaS | Inspetor de requisições em tempo real, regras condicionais, proxy, CRUD stateful, subdomínio por endpoint | Free com limite diário; pago ~US$10–40/mês |
| **Mockoon** | Desktop open source + Cloud | Regras por header/body/query, templating Handlebars + Faker, proxy, gravação, CLI/Docker, import OpenAPI | App grátis; Cloud ~US$15–40/usuário/mês |
| **WireMock Cloud** | SaaS (baseado no WireMock OSS) | Stateful, gravação e playback, fault injection, import OpenAPI/Postman, times | Free limitado; pago a partir de ~US$50/mês |
| **Postman Mock Servers** | Parte do Postman | Mocks a partir de exemplos de collections | Cota de chamadas atrelada ao plano Postman |
| **MockAPI.io** | SaaS | CRUD REST automático com dados fake, relacionamentos | Free; pago ~US$5–35/mês |
| **Mocky / Mockbin / webhook.site** | SaaS simples | Um endpoint rápido; o webhook.site é focado em inspeção | Grátis ou barato |
| **Microcks / Prism (Stoplight)** | Open source | Mock guiado por contrato (OpenAPI/AsyncAPI/gRPC), validação | Grátis (self-host) |
| **json-server / MSW** | Libs OSS | Mock local para devs front-end | Grátis |

**Posicionamento:** hoje o imockey fica entre o Mocky (simples) e o Beeceptor/Mockoon Cloud (completos). Só a simplicidade não sustenta uma mensalidade, porque o Mockoon, o Microcks e o json-server são grátis. Para alguém pagar, o imockey precisa de **colaboração, observabilidade e integração com CI**, que é onde os concorrentes cobram, e de um **ângulo local** que eles não exploram.

## 3. Melhorias sugeridas, por prioridade

### Nível 1: o mínimo para competir (hoje é o que mais faz falta)
1. **Headers de resposta customizados**, incluindo CORS configurável. Sem isso, front-ends no navegador quebram.
2. **Inspetor de requisições:** log em tempo real (o SignalR já existe) com método, path, headers, body, mock casado ou 404 e latência. É o recurso que mais converte no Beeceptor, e a retenção do log é um limite natural para diferenciar planos (24h no free, 30 dias no pro).
3. **Templating rico:** `{{path.id}}`, `{{query.x}}`, `{{header.x}}`, `{{now}}`, `{{randomInt}}` e Faker (`{{faker.name}}`, CPF/CNPJ válidos), além de repetição de arrays.
4. **Importar OpenAPI, Postman e HAR**, gerando os mocks automaticamente. O export já existe, então a base está pronta.

### Nível 2: diferenciais que justificam o plano Pro
5. **Múltiplas respostas por mock com regras:** condições por header, query ou JSONPath do body; modo sequencial (1ª chamada 202, 2ª 200); aleatório ou com pesos.
6. **Simulação de caos:** % de erro, latência com jitter (min–max), timeout e conexão derrubada. Combina com o caso de uso de "simulação de caos" que o README já cita.
7. **Mocks stateful:** recurso CRUD automático (`POST /orders` grava, `GET /orders/{id}` devolve), com reset por API.
8. **Proxy e gravação:** encaminhar o que não casa para a API real e transformar as respostas gravadas em mocks.
9. **Webhooks e callbacks:** o mock dispara um POST assíncrono depois de X segundos. Isso é essencial para simular pagamentos.
10. **API de gerenciamento com tokens, CLI e GitHub Action.** Permite subir e resetar mocks no pipeline de CI. O próprio CLAUDE.md já pede para discutir a autenticação disso antes, e a resposta natural é um token pessoal ou por aplicação.

### Nível 3: plano Team/Business
11. **Organizações e times:** papéis (owner/editor/viewer), aplicações compartilhadas, convites.
12. **Ambientes e versionamento:** histórico de alterações de mocks, rollback, cópias "staging/prod" de uma aplicação.
13. **Subdomínio ou domínio próprio** (`loja.imockey.dev`). Também acaba com o conflito entre slugs e `ReservedRoutes`.
14. **Mocks protegidos:** exigir API key ou Bearer na chamada, ou simular 401/403 de forma realista.
15. **Validação de contrato:** rejeitar requisições fora do schema OpenAPI, o que é útil em testes de integração.
16. **Auditoria, SSO (Google/Microsoft/SAML) e SLA.**

### Diferencial local (a maior vantagem competitiva)
- **Catálogo de mocks prontos de APIs brasileiras:** Pix/BACEN (cobrança + webhook), Open Finance Brasil, ViaCEP, consulta de CNPJ, NF-e/NFS-e, Mercado Pago, Pagar.me, Correios. Nenhum concorrente global oferece isso.
- **Faker brasileiro:** CPF, CNPJ, CEP, telefone e nomes válidos.
- **Cobrança em reais, com Pix e boleto, e nota fiscal.** É uma barreira real para empresas brasileiras pagarem ferramentas em dólar.
- **Dados hospedados no Brasil** (Azure Brazil South) e conformidade com a LGPD, que é um argumento para bancos e fintechs.
- **IA:** "descreva sua API" ou "cole um JSON" e os mocks com dados realistas são gerados.

## 4. Sugestão de planos

| | Free | Pro (~R$29–39/mês) | Team (~R$99–149/mês + por usuário) |
| --- | --- | --- | --- |
| Aplicações / mocks | 2 / 20 | Ilimitado | Ilimitado |
| Requisições/mês | 5 mil | 500 mil | 5 milhões |
| Log de requisições | 24h | 30 dias | 90 dias + exportação |
| Regras, caos, stateful, webhooks | — | ✓ | ✓ |
| API/CLI/CI | — | ✓ | ✓ |
| Times, papéis, auditoria, SSO | — | — | ✓ |
| Domínio próprio | — | Subdomínio | Domínio próprio |
| Atraso máximo (`ProcessingTime`) | 5 s | 60 s | 120 s |

## 5. Ajustes técnicos necessários antes de cobrar

- **Escala:** SQLite com uma réplica e Litestream funcionam bem no começo, mas o middleware consulta o banco **a cada chamada mockada**. Antes de vender planos com volume, adicione um cache em memória dos mocks por slug, invalidado no `OnChange`. Mais adiante será preciso discutir Postgres ou sharding; o CLAUDE.md pede essa conversa antes de trocar o banco.
- **Cotas e rate limiting por aplicação ou plano:** hoje as chamadas são anônimas e ilimitadas, o que gera risco de abuso e de um usuário degradar o serviço para os outros.
- **Teto no `ProcessingTime`:** um atraso longo segura conexões do único processo, o que permite um DoS barato.
- **Billing:** integração com Stripe, Asaas ou Iugu (estes dois com Pix e boleto), mais medição de uso.
- **Vincular conta GitHub e conta com senha:** hoje são contas separadas, e isso confunde quem paga.

**Recomendação:** comece pelo nível 1 (headers, inspetor, templating, import OpenAPI) e por **um** diferencial local forte: mock de Pix com webhook mais Faker brasileiro. Lance o Pro só depois. Esse conjunto já coloca o imockey à frente do Beeceptor e do Mockoon para o público brasileiro.
