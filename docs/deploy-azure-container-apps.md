# Implantação no Azure Container Apps

Passo a passo para hospedar o imockey no **Azure Container Apps** com custo próximo de zero. O guia foi pensado para um uso esporádico: poucas horas por semana e, às vezes, semanas sem nenhum acesso.

## Sumário

- [Como funciona](#como-funciona)
- [Custo esperado](#custo-esperado)
- [Pré-requisitos](#pré-requisitos)
- [1. Publicar a imagem no GitHub Container Registry](#1-publicar-a-imagem-no-github-container-registry)
- [2. Definir as variáveis](#2-definir-as-variáveis)
- [3. Criar o grupo de recursos](#3-criar-o-grupo-de-recursos)
- [4. Criar o storage da réplica do banco](#4-criar-o-storage-da-réplica-do-banco)
- [5. Criar o ambiente do Container Apps sem logs](#5-criar-o-ambiente-do-container-apps-sem-logs)
- [6. Criar o Container App](#6-criar-o-container-app)
- [7. Testar](#7-testar)
- [8. Login com GitHub (opcional)](#8-login-com-github-opcional)
- [9. Alerta de orçamento](#9-alerta-de-orçamento)
- [Atualizar para uma nova versão](#atualizar-para-uma-nova-versão)
- [Operação](#operação)
- [Limitações e cuidados](#limitações-e-cuidados)
- [Remover tudo](#remover-tudo)

---

## Como funciona

```text
Navegador / cliente de mocks
        │  HTTPS
        ▼
Azure Container Apps (ingress)        ← TLS gratuito, escala 0 ↔ 1 réplica
        │  HTTP :8080
        ▼
Container imockey
  ├── litestream replicate -exec "dotnet Imockey.dll"
  │      └── imockey  →  /data/mocks.db (disco efêmero do container)
  └── Litestream replica cada alteração (~1 s) ──►  Azure Blob Storage (container "imockey")
```

- **Sem acesso, o app fica com 0 réplicas** e você não paga computação. A primeira requisição liga o container (partida a frio de alguns segundos).
- **O disco do container é efêmero.** A cada subida, o script [`docker-entrypoint.sh`](../docker-entrypoint.sh) roda `litestream restore`, que baixa o `mocks.db` do Blob Storage. Em seguida, o imockey sobe sob o `litestream replicate`, que envia cada alteração para o Blob em cerca de 1 segundo e faz um sync final quando o container recebe SIGTERM.
- **As chaves de Data Protection** (que assinam o cookie de login) ficam dentro do próprio `mocks.db`. Assim, ninguém é deslogado quando o container é recriado.
- **A imagem fica no GitHub Container Registry** (gratuito para pacotes públicos) e é publicada pelo workflow [`container.yml`](../.github/workflows/container.yml).
- **Os logs do ambiente ficam desligados** (`--logs-destination none`): não há workspace do Log Analytics, que cobra por GB de log ingerido.

---

## Custo esperado

O plano Consumption do Container Apps tem uma **cota gratuita mensal por assinatura**: 180.000 vCPU-segundos, 360.000 GiB-segundos e 2 milhões de requisições.

Com o container no menor tamanho (0,25 vCPU e 0,5 GiB), a cota gratuita cobre cerca de **200 horas ligadas por mês**:

| Uso mensal | vCPU-s | GiB-s | Custo de computação |
| --- | --- | --- | --- |
| ~50 h (12 h/semana) | 45.000 | 90.000 | **US$ 0** (~25% da cota) |
| 200 h | 180.000 | 360.000 | **US$ 0** (limite da cota) |
| 730 h (ligado o mês inteiro) | 657.000 | 1.314.000 | ~US$ 14 no pior caso (ativo o tempo todo) |

Demais itens:

| Item | Custo |
| --- | --- |
| Azure Blob Storage (réplica do banco, alguns MB, Hot LRS) | centavos |
| Operações de escrita no Blob (só quando há alterações) | centavos |
| GitHub Container Registry (pacote público) | grátis |
| Log Analytics | **não usado** |
| Azure Container Registry | **não usado** |
| Tráfego de saída (primeiros 100 GB/mês) | grátis |
| **Total esperado** | **≈ US$ 0 a 0,10 por mês** |

> Os valores são do plano Consumption na data deste guia (outubro de 2026). Confira a [página de preços do Container Apps](https://azure.microsoft.com/pricing/details/container-apps/) antes de implantar.

---

## Pré-requisitos

- Conta Azure com uma assinatura ativa. Mesmo dentro da cota gratuita, a Azure exige um cartão de crédito cadastrado.
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) instalada (`az version`).
- Repositório do imockey no GitHub com Actions habilitado.
- Um terminal **Bash** (Git Bash, WSL ou Linux/macOS). Os comandos abaixo usam sintaxe Bash.

---

## 1. Publicar a imagem no GitHub Container Registry

O workflow [`container.yml`](../.github/workflows/container.yml) roda sozinho depois que o workflow `.NET` (build e testes) passa no `master`. Ele publica duas tags:

- `ghcr.io/<dono>/<repositório>:latest`
- `ghcr.io/<dono>/<repositório>:<sha-curto-do-commit>`

Para a primeira publicação:

1. Faça push para o `master` ou rode o workflow manualmente em **Actions → Container → Run workflow**.
2. Ao final, o pacote aparece em **GitHub → seu perfil → Packages**.
3. **Torne o pacote público**: abra o pacote → **Package settings** → **Change visibility** → **Public**.
   - Pacotes novos nascem privados. Um pacote público dispensa credencial de registry no Azure e é gratuito.
   - A imagem não contém segredos: credenciais entram por variáveis de ambiente e secrets do Container App.

<details>
<summary>Prefere manter o pacote privado?</summary>

Crie um Personal Access Token (classic) no GitHub só com o escopo `read:packages` e, no passo 6, acrescente ao `az containerapp create`:

```bash
  --registry-server ghcr.io \
  --registry-username <seu-usuario-github> \
  --registry-password <token>
```

O token expira: lembre-se de renová-lo (`az containerapp registry set`).
</details>

---

## 2. Definir as variáveis

```bash
RG=rg-imockey
LOCATION=eastus                        # regiões dos EUA costumam ser as mais baratas
STORAGE=stimockey$RANDOM              # nome global único: 3–24 letras minúsculas e números
ENVIRONMENT=cae-imockey
APP=imockey
IMAGE=ghcr.io/phduarte/imockey:latest # ou a tag do commit, ex.: ghcr.io/phduarte/imockey:a1b2c3d

echo "Storage account: $STORAGE"       # anote: ele é necessário em passos futuros
```

O nome do repositório no GHCR é sempre em minúsculas.

---

## 3. Criar o grupo de recursos

```bash
az login
az extension add --name containerapp --upgrade
az provider register --namespace Microsoft.App --wait

az group create --name $RG --location $LOCATION
```

---

## 4. Criar o storage da réplica do banco

```bash
az storage account create \
  --name $STORAGE \
  --resource-group $RG \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2 \
  --access-tier Hot \
  --min-tls-version TLS1_2 \
  --allow-blob-public-access false

STORAGE_KEY=$(az storage account keys list \
  --account-name $STORAGE \
  --resource-group $RG \
  --query "[0].value" -o tsv)

az storage container create \
  --name imockey \
  --account-name $STORAGE \
  --account-key "$STORAGE_KEY"
```

Escolhas de custo:

- **`Standard_LRS`** é a redundância mais barata (3 cópias no mesmo datacenter). Para um projeto experimental é suficiente, e o Litestream ainda guarda 7 dias de histórico (`retention: 168h` em [`litestream.yml`](../litestream.yml)).
- **`Hot`**: a camada Cool cobra mais caro por operação, e o Litestream faz escritas pequenas e frequentes.
- O nome do container de blobs precisa ser **`imockey`**, o valor configurado em [`litestream.yml`](../litestream.yml).

---

## 5. Criar o ambiente do Container Apps sem logs

```bash
az containerapp env create \
  --name $ENVIRONMENT \
  --resource-group $RG \
  --location $LOCATION \
  --logs-destination none
```

`--logs-destination none` evita a criação de um workspace do Log Analytics. Esse workspace é o custo escondido mais comum do Container Apps, porque cobra por GB de log ingerido.

Sem ele, os logs do console **não são armazenados**. O streaming em tempo real, enquanto há réplica rodando, continua disponível (veja [Operação](#operação)).

---

## 6. Criar o Container App

```bash
az containerapp create \
  --name $APP \
  --resource-group $RG \
  --environment $ENVIRONMENT \
  --image $IMAGE \
  --ingress external \
  --target-port 8080 \
  --cpu 0.25 \
  --memory 0.5Gi \
  --min-replicas 0 \
  --max-replicas 1 \
  --secrets storage-key="$STORAGE_KEY" \
  --env-vars \
    AZURE_STORAGE_ACCOUNT=$STORAGE \
    LITESTREAM_AZURE_ACCOUNT_KEY=secretref:storage-key
```

| Parâmetro | Por quê |
| --- | --- |
| `--cpu 0.25 --memory 0.5Gi` | Menor tamanho disponível: a cota gratuita rende cerca de 200 h por mês |
| `--min-replicas 0` | Desliga quando não há tráfego (após ~5 minutos ocioso); é isso que zera o custo |
| `--max-replicas 1` | **Obrigatório.** Duas réplicas teriam duas cópias do SQLite e uma sobrescreveria a outra no Blob |
| `--target-port 8080` | Porta padrão do ASP.NET nas imagens oficiais do .NET |
| `--secrets` + `secretref:` | A chave do storage fica como secret, não como texto na configuração |

O ingress do Container Apps já fornece HTTPS com certificado gerenciado e suporta WebSocket, necessário para o Blazor.

---

## 7. Testar

```bash
FQDN=$(az containerapp show \
  --name $APP \
  --resource-group $RG \
  --query properties.configuration.ingress.fqdn -o tsv)

echo "https://$FQDN"
```

1. Abra `https://$FQDN`. A primeira requisição pode levar alguns segundos (partida a frio).
2. Crie uma conta, uma aplicação e um mock.
3. Chame o mock: `curl https://$FQDN/<slug>/<pattern>`.
4. Confira a réplica no Blob:

   ```bash
   az storage blob list \
     --container-name imockey \
     --account-name $STORAGE \
     --account-key "$STORAGE_KEY" \
     --query "[].name" -o tsv
   ```

5. **Teste a recuperação**: force uma nova réplica e veja se os dados continuam lá.

   ```bash
   az containerapp revision restart \
     --name $APP \
     --resource-group $RG \
     --revision $(az containerapp revision list --name $APP --resource-group $RG --query "[0].name" -o tsv)
   ```

   Recarregue a página. Você deve continuar logado e com os mesmos mocks.

---

## 8. Login com GitHub (opcional)

1. Em **GitHub → Settings → Developer settings → OAuth Apps → New OAuth App**:
   - **Homepage URL**: `https://<FQDN>`
   - **Authorization callback URL**: `https://<FQDN>/signin-github`
2. Configure no Container App:

   ```bash
   az containerapp secret set \
     --name $APP \
     --resource-group $RG \
     --secrets github-client-secret=<client-secret>

   az containerapp update \
     --name $APP \
     --resource-group $RG \
     --set-env-vars \
       Authentication__GitHub__ClientId=<client-id> \
       Authentication__GitHub__ClientSecret=secretref:github-client-secret
   ```

O callback usa HTTPS corretamente porque a imagem define `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: o app respeita o `X-Forwarded-Proto` enviado pelo ingress.

---

## 9. Alerta de orçamento

Mesmo com custo esperado zero, configure um alerta para ser avisado de qualquer surpresa:

1. Portal Azure → **Cost Management + Billing** → **Budgets** → **Add**.
2. Escopo: a assinatura (ou o grupo `rg-imockey`).
3. Valor: **US$ 5 por mês** (ou o que fizer sentido para você).
4. Alertas: 50% e 100% do **custo real** e 100% do **custo previsto**, enviados para o seu e-mail.

O orçamento só avisa; ele não desliga nada.

---

## Atualizar para uma nova versão

Depois que o workflow publicar uma nova imagem:

```bash
az containerapp update \
  --name $APP \
  --resource-group $RG \
  --image ghcr.io/phduarte/imockey:<sha-curto-do-commit>
```

Prefira a **tag do commit** à `latest`. Uma nova tag gera uma nova revisão e deixa claro qual versão está rodando. Com `latest`, o Container App não percebe que a imagem mudou.

Faça a atualização **quando ninguém estiver usando**. Durante a troca de revisão, a antiga e a nova podem ficar ligadas por alguns segundos, e alterações feitas nesse intervalo na revisão antiga podem se perder (veja [Limitações](#limitações-e-cuidados)).

---

## Operação

### Ver logs em tempo real

```bash
az containerapp logs show --name $APP --resource-group $RG --follow
```

Funciona apenas enquanto há uma réplica rodando. Os logs não são armazenados.

### Ver se o app está ligado

```bash
az containerapp replica list --name $APP --resource-group $RG -o table
```

Lista vazia significa que o app está em zero réplicas e não está consumindo computação.

### Baixar uma cópia do banco (ou voltar no tempo)

Com o Docker instalado, use a própria imagem para restaurar a réplica num arquivo local:

```bash
docker run --rm \
  -v "$PWD:/out" \
  -e AZURE_STORAGE_ACCOUNT=$STORAGE \
  -e LITESTREAM_AZURE_ACCOUNT_KEY="$STORAGE_KEY" \
  --entrypoint litestream \
  $IMAGE \
  restore -o /out/mocks.db /data/mocks.db
```

Acrescente `-timestamp 2026-10-10T12:00:00Z` antes de `/data/mocks.db` para restaurar o estado de um momento específico (dentro dos 7 dias de retenção).

### Zerar o banco

1. Espere o app ficar com zero réplicas (`az containerapp replica list` vazio).
2. Apague a réplica:

   ```bash
   az storage blob delete-batch \
     --source imockey \
     --account-name $STORAGE \
     --account-key "$STORAGE_KEY"
   ```

3. Na próxima requisição, o app sobe com um banco novo e vazio.

### Rotacionar a chave do storage

```bash
az storage account keys renew --account-name $STORAGE --resource-group $RG --key primary
NEW_KEY=$(az storage account keys list --account-name $STORAGE --resource-group $RG --query "[0].value" -o tsv)
az containerapp secret set --name $APP --resource-group $RG --secrets storage-key="$NEW_KEY"
```

Depois, reinicie a revisão (passo 7, item 5) para o container ler a nova chave.

---

## Limitações e cuidados

- **Partida a frio.** Depois de um período parado, a primeira requisição leva alguns segundos: o container sobe, restaura o banco e inicia o .NET. Testes automatizados que chamam mocks com timeout curto podem falhar nessa primeira chamada. Faça uma chamada de "aquecimento" antes, se necessário.
- **Aba aberta mantém o app ligado.** A UI em Blazor mantém um WebSocket aberto. Enquanto alguma aba estiver aberta, o app não desliga e consome a cota. Feche a aba quando terminar.
- **Uma réplica só.** Nunca aumente `--max-replicas` para mais de 1. O SQLite e o Litestream assumem um único processo escrevendo no banco.
- **Troca de revisão.** Durante uma atualização, duas revisões podem coexistir por alguns segundos. Atualize com o app ocioso.
- **Perda máxima em falha abrupta: cerca de 1 segundo** de alterações (o intervalo de sync do Litestream). Em encerramentos normais (scale to zero, atualização), o sync final não perde nada.
- **Pacote GHCR público** significa que qualquer pessoa pode baixar a imagem. Ela não tem segredos, mas, se o repositório for privado, use a opção de [pacote privado](#1-publicar-a-imagem-no-github-container-registry).

---

## Remover tudo

```bash
az group delete --name $RG --yes
```

Isso apaga o Container App, o ambiente e o storage, **incluindo a réplica do banco**. Baixe uma cópia antes, se quiser guardar os dados.
