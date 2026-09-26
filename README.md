# OrderFlow FIX

Aplicação para envio de ordens por FIX 4.4, cálculo de exposição financeira por cliente e ativo e acompanhamento de auditoria pelo Kafka.

> This is a challenge by [Coodesh](https://coodesh.com/)

## Tecnologias

- C#, .NET 8 e ASP.NET Core com controllers e Swagger.
- React, TypeScript, Vite e Nginx.
- PostgreSQL, Entity Framework Core e migrações versionadas.
- QuickFIX/n, FIX 4.4 e Apache Kafka.
- xUnit, Vitest, Testing Library e Testcontainers.
- Docker e Docker Compose.

## Arquitetura

A solução segue Clean Architecture, com responsabilidades separadas:

- **Domain:** entidades e regras financeiras, sem dependência de banco ou
  transporte.
- **Application:** casos de uso e interfaces de persistência, comunicação e
  obtenção de limites.
- **Infrastructure:** implementações PostgreSQL, FIX, Kafka e provider de
  limite.
- **OrderGenerator:** API HTTP para o React, envio das ordens e consulta de
  resultados e auditoria.
- **OrderAccumulator:** processamento das ordens, controle de exposição e
  publicação das decisões.

O frontend acessa somente a API do Generator. As ordens e suas decisões trafegam
entre os serviços por FIX 4.4. O Kafka alimenta a auditoria, que pode chegar
depois do resultado da ordem.

O Generator grava a solicitação e a pendência de envio na mesma transação. O
Accumulator persiste decisão, exposição e outbox de forma atômica. O publisher
envia os eventos ao Kafka após o commit, e o consumidor usa inbox para evitar
auditoria duplicada.

A idempotência usa identificadores persistidos; a concorrência é controlada por
cliente e ativo no PostgreSQL. Timeout mantém o resultado pendente. Outbox e
Kafka não garantem, isoladamente, processamento exatamente uma vez.

## Instalação e execução

Requisito para executar a solução: Docker Desktop ou Docker Engine com Compose.

Na raiz do repositório, copie `.env.example` para `.env` e ajuste as credenciais
locais. No PowerShell:

```powershell
Copy-Item .env.example .env
docker compose -f compose.yaml -f compose.dev.yaml up --build -d
docker compose ps
```

No Linux ou macOS, use `cp .env.example .env` para copiar o arquivo. Preserve o
`.env` se ele já existir.

- [Frontend](http://localhost:3000)
- [Swagger em desenvolvimento](http://localhost:5080/swagger)

O Compose inicia banco, Kafka e aplicações. As migrações são aplicadas na
inicialização com `Database.MigrateAsync()`. Cada serviço acessa seu próprio
schema no PostgreSQL.

Para consultar logs e encerrar a execução:

```bash
docker compose logs -f ordergenerator orderaccumulator
docker compose down
```

Os volumes preservam os registros após `down`. O serviço `kafka-storage-init`
encerra após preparar o volume; aparecer parado é esperado.

## Interface e API

A tela contém negociação, acompanhamento, exposição do cliente, ordens paginadas
e linha do tempo de auditoria. Os erros de preenchimento e validação da API são
exibidos de forma amigável.

Principais endpoints do Generator:

- `POST /api/orders`: envia ou recupera uma solicitação pelo mesmo ClOrdID.
- `GET /api/orders`: lista ordens com `accountId`, `page` e `pageSize`.
- `GET /api/orders/{clOrdId}`: consulta o resultado persistido.
- `GET /api/orders/{clOrdId}/events`: consulta a auditoria.
- `GET /api/accounts`: lista clientes demonstrativos.
- `GET /api/accounts/{accountId}/exposures`: consulta exposição e limite.

Os dois processos disponibilizam `/health/live` e `/health/ready`. Consulte os
contratos HTTP no Swagger do Generator.

## Testes

Para desenvolvimento e testes locais: SDK .NET 8 compatível com `global.json`,
Node.js 22.12 ou superior e npm.

Na raiz:

```bash
dotnet restore OrderFlow.sln
dotnet build OrderFlow.sln
dotnet test tests/OrderFlow.UnitTests
dotnet test tests/OrderFlow.IntegrationTests
```

Os testes unitários cobrem validação, exposição, obtenção de limites,
idempotência e contratos HTTP. Os testes de integração usam PostgreSQL, FIX e
Kafka e exigem Docker disponível.

Para o frontend:

```bash
cd frontend
npm ci
npm test
npm run build
```

Para experimentar a interface com dados simulados, execute `npm run dev:mock`
nessa pasta. O build de produção utiliza a API real.

## Logs e segurança

Os dois serviços usam logs JSON. Configure os níveis em `Logging:LogLevel` no
respectivo `appsettings.json`, ou por variáveis de ambiente. As chamadas das
controllers registram informação, falhas de validação e erros com contexto da
requisição.

A API aplica validação, limites de requisições e tamanho do corpo, CORS restrito
e respostas ProblemDetails. O `.gitignore` exclui credenciais locais e arquivos
gerados.

As contas são demonstrativas, sem autenticação ou autorização por cliente.
Produção exige controle de acesso, TLS e gestão segura de segredos e das
conexões entre serviços.

## Organização do repositório

- `src/OrderFlow.Domain`: domínio.
- `src/OrderFlow.Application`: casos de uso e contratos.
- `src/OrderFlow.Infrastructure`: integrações e persistência.
- `src/OrderGenerator` e `src/OrderAccumulator`: hosts, controllers e
  configuração.
- `frontend`: interface React.
- `tests`: testes de backend.
- `config` e `deploy`: configuração de comunicação e inicialização dos
  containers.
- `docs`: documentação complementar.
