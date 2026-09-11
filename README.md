# Mazza Orders API

Backend de gestão de pedidos para e-commerce, em .NET 10, com Clean Architecture,
CQRS via MediatR, EF Core + SQLite, autenticação JWT e validação em pipeline.

O foco do teste é qualidade arquitetural, então este README explica **o que foi feito
e por quê** — as decisões estão documentadas aqui e nos comentários do código, nos
pontos em que a escolha não é óbvia.

---

## Sumário

- [Como rodar](#como-rodar)
- [Endpoints](#endpoints)
- [Exemplo completo com curl](#exemplo-completo-com-curl)
- [Arquitetura](#arquitetura)
- [Decisões técnicas](#decisões-técnicas)
- [Regras de negócio](#regras-de-negócio)
- [Tratamento de erros](#tratamento-de-erros)
- [Testes](#testes)
- [Observabilidade](#observabilidade)
- [Análise estática (SonarQube)](#análise-estática-sonarqube)
- [Segurança](#segurança)
  - [A chave de assinatura](#a-chave-de-assinatura)
- [O que eu faria diferente em produção](#o-que-eu-faria-diferente-em-produção)

---

## Como rodar

### Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) (validado com `10.0.201`)
- Docker + Docker Compose (apenas para o caminho conteinerizado)

Não é necessário instalar nem configurar nada:

- o SQLite é um arquivo, e as **migrations são aplicadas automaticamente na
  inicialização**;
- **não há chave JWT para configurar** em Development — a aplicação gera uma aleatória
  por processo, porque não existe segredo nenhum versionado neste repositório. Fora de
  Development ela é obrigatória e a aplicação não sobe sem ela. Ver
  [A chave de assinatura](#a-chave-de-assinatura).

### Local

```bash
git clone <repo> && cd MazzaTeste

dotnet restore
dotnet build
dotnet test                                  # 117 testes

dotnet run --project src/Mazza.Orders.Api
```

A API sobe em `http://localhost:5104` (ver `Properties/launchSettings.json`).

| Recurso                | URL                                    |
| ---------------------- | -------------------------------------- |
| API explorer (Scalar)  | http://localhost:5104/scalar/v1        |
| Documento OpenAPI      | http://localhost:5104/openapi/v1.json  |
| Health check           | http://localhost:5104/health           |

Para fixar a porta: `dotnet run --project src/Mazza.Orders.Api --urls http://localhost:5199`.

### Docker

```bash
docker compose up --build
```

A API fica em `http://localhost:8080` (explorer em http://localhost:8080/scalar/v1).
Nenhuma variável precisa ser definida; para fixar a chave de assinatura, copie
`.env.example` para `.env` e preencha `JWT_SIGNING_KEY`.

O banco fica em um volume nomeado (`orders-data`), então `docker compose down`
preserva os pedidos — use `docker compose down --volumes` para descartar.

```bash
docker compose logs -f api      # logs estruturados + traces do OpenTelemetry
docker compose down
```

### Migrations

Aplicadas sozinhas no startup. Para mexer nelas manualmente:

```bash
dotnet tool restore                          # instala o dotnet-ef pinado em .config/

dotnet ef migrations add <Nome> \
  --project src/Mazza.Orders.Infrastructure \
  --output-dir Persistence/Migrations

dotnet ef database update --project src/Mazza.Orders.Infrastructure
```

Não é preciso passar `--startup-project`: existe um `IDesignTimeDbContextFactory`
(`OrdersDbContextFactory`) que torna a operação autocontida na Infrastructure.

---

## Endpoints

| Método  | Rota                        | Auth | Descrição                                        |
| ------- | --------------------------- | :--: | ------------------------------------------------ |
| `POST`  | `/auth/login`               |  —   | Troca credenciais por um JWT                      |
| `POST`  | `/api/orders`               |  ✔   | Cria um pedido → `201` + `Location`              |
| `GET`   | `/api/orders`               |  ✔   | Lista paginada: `?page=1&pageSize=10`            |
| `GET`   | `/api/orders/{id}`          |  ✔   | Retorna um pedido com seus itens                  |
| `PATCH` | `/api/orders/{id}/cancel`   |  ✔   | Cancela um pedido pendente                        |
| `GET`   | `/health`                   |  —   | Liveness probe                                    |

**Usuário fixo:** `dev@martech.com` / `Senha@123`

`pageSize` tem teto de 100 — sem limite, `?pageSize=1000000` é um vetor de negação de
serviço barato contra o banco e o serializador.

---

## Exemplo completo com curl

```bash
BASE=http://localhost:5104

# 1. login
TOKEN=$(curl -s -X POST $BASE/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"dev@martech.com","password":"Senha@123"}' \
  | jq -r .accessToken)

# 2. cria um pedido
ORDER_ID=$(curl -s -X POST $BASE/api/orders \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{
        "customerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "items": [
          { "productName": "Monitor 27\"", "quantity": 2, "unitPrice": 1250.50 },
          { "productName": "Cabo HDMI",    "quantity": 3, "unitPrice": 19.90 }
        ]
      }' | jq -r .id)

# 3. consulta
curl -s $BASE/api/orders/$ORDER_ID -H "Authorization: Bearer $TOKEN" | jq

# 4. lista paginada
curl -s "$BASE/api/orders?page=1&pageSize=10" -H "Authorization: Bearer $TOKEN" | jq

# 5. cancela
curl -s -X PATCH $BASE/api/orders/$ORDER_ID/cancel -H "Authorization: Bearer $TOKEN" | jq

# 6. cancelar de novo → 409 Conflict
curl -s -X PATCH $BASE/api/orders/$ORDER_ID/cancel -H "Authorization: Bearer $TOKEN" | jq
```

Resposta do passo 2 (`201 Created`, `Location: /api/orders/{id}`):

```json
{
  "id": "01a08b4a-65e6-754c-9bf8-580b8d922c01",
  "customerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Pending",
  "createdAt": "2026-09-10T12:28:23.9107052Z",
  "totalAmount": 2560.70,
  "items": [
    { "id": "…", "productName": "Monitor 27\"", "quantity": 2, "unitPrice": 1250.50, "lineTotal": 2501.00 },
    { "id": "…", "productName": "Cabo HDMI",    "quantity": 3, "unitPrice": 19.90,   "lineTotal": 59.70 }
  ]
}
```

Resposta do passo 4:

```json
{
  "items": [ { "id": "…", "status": "Pending", "totalAmount": 2560.70, "itemCount": 2 } ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 3,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

---

## Arquitetura

```
src/
├── Mazza.Orders.Domain           ← zero dependências, nem NuGet
│   ├── Common/                     Entity, DomainException, InvalidOrderStateException
│   └── Orders/                     Order (aggregate root), OrderItem, OrderStatus
│
├── Mazza.Orders.Application      ← MediatR + FluentValidation, nada de EF/ASP.NET
│   ├── Common/Abstractions/        portas: IOrderRepository, IUnitOfWork,
│   │                               IUserAuthenticator, IAccessTokenFactory
│   ├── Common/Behaviors/           LoggingBehavior, ValidationBehavior
│   ├── Orders/Commands|Queries/    um diretório por caso de uso
│   └── Authentication/
│
├── Mazza.Orders.Infrastructure   ← EF Core, SQLite, JWT, PBKDF2
│   ├── Persistence/                DbContext, configurations, repository, migrations
│   └── Identity/                   JwtAccessTokenFactory, InMemoryUserAuthenticator
│
└── Mazza.Orders.Api              ← Minimal API: endpoints, ProblemDetails, OTel
    ├── Endpoints/
    ├── Errors/                     GlobalExceptionHandler
    └── OpenApi/

tests/
├── Mazza.Orders.UnitTests        ← 92 testes: domínio, handlers, validators, behavior
└── Mazza.Orders.IntegrationTests ← 25 testes: WebApplicationFactory sobre HTTP real
```

O sentido das dependências é garantido pelos **`ProjectReference`**, não por
convenção: `Application.csproj` simplesmente não referencia EF Core, então a
afirmação "a camada de aplicação não conhece o banco" é verificável pelo compilador,
não uma promessa no README.

```
Api ──► Application ──► Domain
 │            ▲
 └──► Infrastructure ──┘        (só para o AddInfrastructure() no composition root)
```

Cada camada expõe **um** método de registro (`AddApplication()`,
`AddInfrastructure()`, `AddApiServices()`), então o `Program.cs` lê como uma lista de
intenções em vez de oitenta linhas de wiring.

---

## Decisões técnicas

### Minimal API em vez de Controllers

Este serviço tem cinco endpoints, e cada handler HTTP é um adaptador de duas linhas:
monta o request, manda para o MediatR, escolhe o status code. Controllers trariam
model binding por atributo, filtros, convenções e um ciclo de vida de classe que
nada disso usa.

O que normalmente se perde com Minimal API — organização e descoberta — é resolvido
com `MapGroup` por recurso em `Endpoints/*.cs`: o `RequireAuthorization()` fica no
grupo, então **um endpoint novo já nasce protegido** em vez de depender de alguém
lembrar do atributo. É a mesma agrupação que um controller daria, sem o peso.

Se o serviço crescesse para dezenas de rotas com versionamento, content negotiation e
filtros compartilhados, Controllers passariam a valer o custo.

### CQRS com MediatR, sem event sourcing

Commands e queries são separados fisicamente (`Orders/Commands/`, `Orders/Queries/`)
e retornam tipos diferentes: `OrderDto` completo na escrita e no get-by-id,
`OrderSummaryDto` mais leve na listagem.

O ganho real aqui não é "CQRS" como sigla, e sim o **pipeline**: validação e logging
são behaviors, então valem para todo request existente e futuro sem que nenhum
handler precise chamá-los. Um handler novo já vem validado e instrumentado.

Não há barramento de eventos, projeções nem read store separado — isso seria CQRS de
verdade e não se paga em um domínio deste tamanho.

### MediatR fixado em 12.5.0

Última versão sob licença Apache-2.0. Da v13 em diante o MediatR exige chave de
licença comercial em runtime. Fixado em `Directory.Packages.props`, com o motivo
escrito lá.

### `TotalAmount` calculado no domínio

O enunciado pede explicitamente que o total seja calculado no domínio. Ele é uma
**propriedade computada** de `Order`:

```csharp
public decimal TotalAmount => _items.Sum(item => item.LineTotal);
```

Assim existe uma definição única de "quanto custa este pedido", impossível de
divergir dos itens que ela resume.

**Trade-off assumido:** como o total não é coluna, os caminhos de leitura precisam
carregar os itens (`.Include(o => o.Items)`) em vez de projetar o total em SQL. Com
paginação de 10 a 100 linhas isso é irrelevante. Se a listagem crescesse para
milhões de pedidos, a alternativa seria manter `TotalAmount` como coluna
desnormalizada recalculada pelo agregado a cada mudança de itens — mais rápido para
ler, com risco de drift. Preferi a versão sem drift enquanto o volume não exige a
outra.

### Sem repositório genérico

`IOrderRepository` tem exatamente três métodos, cada um nomeado pela intenção:
`Add`, `FindByIdAsync`, `GetPageAsync`. Não existe `IRepository<T>` porque:

- ele vazaria `IQueryable`/predicados para os handlers, junto com a semântica do EF
  Core que a abstração deveria esconder;
- qualquer chamador poderia consultar qualquer agregado de qualquer forma, o que
  dissolve o limite do aggregate;
- não há um segundo agregado para compartilhar código.

Pelo mesmo motivo, só o aggregate root tem `DbSet`. Não existe `DbSet<OrderItem>`:
um item só é alcançável através do pedido que o possui.

### `IUnitOfWork` separado do repositório

O repositório responde "como chego neste agregado"; o unit of work responde "quando
este trabalho se torna durável". Separar deixa a decisão de commit no handler, o
único lugar que sabe que um caso de uso terminou.

O `OrdersDbContext` implementa `IUnitOfWork` diretamente, sem uma classe
`EfUnitOfWork` no meio: um DbContext **já é** um unit of work, e um wrapper cujo
corpo é `=> _context.SaveChangesAsync()` acrescentaria um arquivo sem acrescentar uma
costura.

### `TimeProvider` em vez de `DateTime.UtcNow`

`Order.Create` recebe o instante de criação; os handlers o obtêm do `TimeProvider`
(BCL, .NET 8+) injetado. O relógio é uma dependência como qualquer outra, e por isso
`CreatedAt` pode ser asserido com igualdade exata nos testes em vez de com tolerância.

### Validação em duas camadas — de propósito

`CreateOrderCommandValidator` e os guards dentro de `Order`/`OrderItem` checam as
mesmas regras. A duplicação se paga:

- o **validator** existe para responder ao cliente com a lista completa de campos
  inválidos em um único `400`;
- os **guards do agregado** existem para tornar o invariante impossível de violar por
  qualquer caminho, inclusive código futuro que esqueça de validar.

Nenhum dos dois pode ser removido em favor do outro sem perder algo. Há teste para
os dois lados: `CreateOrderCommandHandlerTests` chama o handler direto, sem pipeline,
e confirma que o domínio recusa e nada é comitado.

### Enums serializados como texto

`status` sai como `"Pending"` na API e é gravado como `TEXT` no SQLite. Custa alguns
bytes e entrega um contrato autodescritivo que não quebra se os valores numéricos do
enum forem reordenados — e um banco legível com o CLI do sqlite às duas da manhã.

### Commands reaproveitados como contrato HTTP

`POST /auth/login` e `POST /api/orders` fazem bind direto em `LoginCommand` e
`CreateOrderCommand`. O command **é** o contrato de entrada do caso de uso;
duplicá-lo em um record de request da API criaria dois tipos para manter em sincronia
sem nenhuma flexibilidade extra. A dependência continua apontando para dentro
(Api → Application).

Quando a API precisar versionar independentemente do caso de uso, aí os contratos se
separam — e os testes de integração já protegem esse momento, porque declaram as
formas de resposta localmente em vez de desserializar nos DTOs da Application.

### Sem AutoMapper

Mapeamento explícito em `OrderMappings`. Para um punhado de tipos, ele é mais curto
de ler do que a configuração que substituiria, quebra em tempo de compilação quando
uma propriedade é renomeada, e não deixa nada para o revisor adivinhar.

---

## Regras de negócio

Todas vivem no domínio, nenhuma em controller ou em Infrastructure.

| Regra                                              | Onde                             |
| -------------------------------------------------- | -------------------------------- |
| Pedido precisa de ao menos 1 item                  | `Order.Create`                   |
| `Quantity > 0`                                     | `OrderItem` (construtor)         |
| `UnitPrice > 0`                                    | `OrderItem` (construtor)         |
| Só pedidos `Pending` podem ser cancelados          | `Order.Cancel`                   |
| Só pedidos `Pending` podem ser confirmados         | `Order.Confirm`                  |
| `TotalAmount = Σ (UnitPrice × Quantity)`           | `Order.TotalAmount`              |
| `ProductName` obrigatório, ≤ 200 caracteres        | `OrderItem` (construtor)         |
| `UnitPrice` com no máximo 2 casas decimais         | `CreateOrderCommandValidator`    |

A última é a única que só existe na borda, e por um motivo concreto: dinheiro é
`decimal(18,2)`. Sem essa regra, `10.999` passaria e seria truncado em silêncio na
gravação — o total persistido discordaria do que o cliente recebeu na resposta.

---

## Tratamento de erros

Um único `IExceptionHandler` (`GlobalExceptionHandler`) traduz exceções em respostas
[RFC 9457 ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457). É o único ponto da
solução que conhece status codes de falha — nenhum endpoint tem `try/catch`, e é por
isso que cada um cabe em duas linhas.

| Exceção                        | HTTP  | Por quê                                                     |
| ------------------------------ | ----- | ----------------------------------------------------------- |
| `ValidationException`          | `400` | Payload malformado; retorna todos os campos de uma vez      |
| `AuthenticationFailedException`| `401` | Credencial rejeitada                                        |
| `NotFoundException`            | `404` | Agregado não existe                                         |
| `InvalidOrderStateException`   | `409` | Request válido, recurso em estado incompatível              |
| `DomainException`              | `422` | Sintaticamente válido, semanticamente inaceitável           |
| qualquer outra                 | `500` | Bug; logado inteiro, resposta não revela nada               |

O `409` do cancelamento duplicado é a distinção que mais importa: não é `400`, porque
o request estava perfeitamente bem formado — o pedido apenas não está mais em um
estado cancelável. O mesmo request poderia ter dado certo antes.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The order is not in a state that allows this operation.",
  "status": 409,
  "detail": "Only pending orders can be cancelled. Order 01a08b4a-… is Cancelled.",
  "traceId": "00-d7a23ec253eb6359239878bb387ede28-81a511e8cc626b33-01"
}
```

O `traceId` é o mesmo trace do OpenTelemetry, então um erro reportado por um cliente
liga direto no trace e nos logs do request.

---

## Testes

```bash
dotnet test                                              # 117 testes
dotnet test tests/Mazza.Orders.UnitTests                 # 92
dotnet test tests/Mazza.Orders.IntegrationTests          # 25

dotnet test --collect:"XPlat Code Coverage"              # cobertura
```

**xUnit + NSubstitute.** Sem FluentAssertions: a v8 mudou para licença comercial, e
os asserts do próprio xUnit dão conta.

### Unitários (92)

| Arquivo                              | O que cobre                                              |
| ------------------------------------ | -------------------------------------------------------- |
| `OrderTests`                         | Invariantes, transições de estado, cálculo do total      |
| `CreateOrderCommandHandlerTests`     | Persiste, comita uma vez, total do domínio, clock fixo   |
| `CancelOrderCommandHandlerTests`     | Cancela, 404, estado inválido, token propagado           |
| `GetOrdersQueryHandlerTests`         | Projeção e metadados de paginação                        |
| `GetOrderByIdQueryHandlerTests`      | Encontrado / não encontrado                              |
| `LoginCommandHandlerTests`           | Credencial ok/ruim, `expiresIn` derivado do relógio      |
| `CreateOrderCommandValidatorTests`   | Cada campo, incluindo escala decimal                     |
| `GetOrdersQueryValidatorTests`       | Limites de paginação                                     |
| `ValidationBehaviorTests`            | Handler **não** roda quando a validação falha            |

Os testes de domínio são os mais valiosos do conjunto: não usam mock nem banco,
porque as regras que eles exercitam moram numa classe sem dependência alguma.

Alguns asserts existem para proteger detalhes que passam batido em review:

- `Handle_PropagatesTheCancellationTokenToThePersistenceLayer` — token perdido
  significa request cancelado ainda segurando conexão de banco;
- `Handle_WithAnAlreadyExpiredToken_ReportsZeroRatherThanANegativeLifetime`;
- `Cancel_OnAConfirmedOrder_IsRejected` — e o status **permanece** `Confirmed`: uma
  transição recusada não pode deixar o agregado meio alterado.

### Integração (25)

`WebApplicationFactory<Program>` sobe a aplicação real em processo. A **única** coisa
trocada é o caminho do arquivo SQLite; o resto — grafo de DI, pipeline do MediatR,
validação de JWT, exception handler, provider real do SQLite e as migrations reais —
é exatamente o que roda em produção.

Arquivo temporário em vez de `:memory:`: um SQLite em memória vive e morre com uma
única conexão, enquanto o EF abre e fecha conexões por operação.

Cobrem o que teste unitário estruturalmente não alcança:

- `MigrationsAreAppliedOnStartup` — sem elas não haveria tabela `Orders` e a primeira
  query falharia em vez de devolver página vazia;
- `EveryOrderEndpointRequiresAToken` (Theory nas 4 rotas) e `AGarbageTokenIsRejected`;
- `CreateThenGetById_RoundTripsThroughSqliteUnchanged` — dinheiro atravessa EF e
  SQLite e volta idêntico;
- `Cancel_Twice_Returns409`, `Cancel_OnAPendingOrder_...` verificando que o
  cancelamento **persistiu**, não só foi reportado;
- `GetOrders_PaginatesAndNeverRepeatsAnOrderAcrossPages` — a razão de existir o
  desempate por id: sem ele um pedido poderia aparecer em duas páginas ou em nenhuma.

---

## Observabilidade

### Serilog + logging behavior

`LoggingBehavior` loga todo command/query com nome e tempo de execução, via
`ILogger<T>` — **não** via API do Serilog. A camada de aplicação não pode saber qual
biblioteca de log o host escolheu; o Serilog entra como provider no composition root.
Trocá-lo não tocaria naquele arquivo.

Duas decisões dentro dele:

1. **Loga o tipo do request, nunca o payload.** Um behavior que despeja o request
   inteiro escreveria `LoginCommand.Password` em texto plano no log a cada login.
2. **Usa delegates gerados por `[LoggerMessage]`**, porque isso roda em todo request
   e não vale re-parsear o template nem fazer boxing dos argumentos.

```
[09:28:23 INF] Handling CreateOrderCommand
[09:28:23 INF] Handled CreateOrderCommand in 24 ms
[09:28:23 INF] HTTP POST /api/orders responded 201 in 31.4482 ms
```

### OpenTelemetry

Traces e métricas de ASP.NET Core e HttpClient, com exporter de console.

Fica atrás de uma flag (`Telemetry:ConsoleExporterEnabled`) porque o exporter de
console é genuinamente barulhento — despeja um bloco de métricas a cada intervalo de
coleta e afoga os logs de request ao lado. Ligado no container (onde é o que se quer
ver) e desligado em `appsettings.Development.json` (onde atrapalha quem está
trabalhando).

Trocar console por OTLP é mudar `AddConsoleExporter()` por `AddOtlpExporter()`.

---

## Análise estática (SonarQube)

Atrás de um profile, porque o SonarQube quer ~2 GB de RAM e um minuto para subir —
ninguém quer isso imposto só para rodar a API.

```bash
docker compose --profile sonar up -d sonarqube
# crie um token de projeto em http://localhost:9000  (admin / admin)
SONAR_TOKEN=<token> docker compose --profile sonar run --rm sonar-scanner
```

O serviço `sonar-scanner` é construído a partir de `docker/sonar-scanner.Dockerfile`
porque a análise precisa das duas toolchains na mesma imagem: o SDK .NET para
compilar e coletar cobertura, e um JRE porque o engine do SonarScanner é Java —
nenhuma imagem oficial tem os dois.

`docker/sonar-scan.sh` roda `begin → build → test → end` nessa ordem: o build
**precisa** acontecer entre `begin` e `end`, senão o scanner não intercepta o MSBuild
e produz uma análise sem nenhum C#. A cobertura é coletada em formato **OpenCover** —
o cobertura, default do coverlet, é ignorado em silêncio pelo SonarQube.

### Qualidade no build

Independente do Sonar, o build já é o primeiro gate:

- `TreatWarningsAsErrors` + `AnalysisLevel=latest-recommended` em todos os projetos;
- toda regra afrouxada está no `.editorconfig` **com o motivo escrito** — nada de
  supressão silenciosa;
- Central Package Management (`Directory.Packages.props`): cada versão existe em um
  único arquivo, então duas camadas não podem divergir de build da mesma dependência.

Os analisadores já pegaram coisa real durante o desenvolvimento — foi o `CA1873` que
apontou o `LoggingBehavior` avaliando argumentos caros de log, o que levou aos
delegates gerados e à decisão de não logar payload.

---

## Segurança

Um usuário fixo em memória era suficiente pelo enunciado, mas alguns detalhes foram
feitos "do jeito certo" porque o custo é baixo e o hábito errado é caro:

- **Senha nunca comparada com `==`.** `Pbkdf2PasswordHasher` usa PBKDF2-HMAC-SHA256
  com 210.000 iterações (recomendação OWASP) e compara com
  `CryptographicOperations.FixedTimeEquals`. Uma comparação comum retorna no primeiro
  byte diferente e vaza pelo tempo quanto do palpite estava certo.
- **Login não revela quais contas existem.** O hash é verificado mesmo quando o
  e-mail não bate, então usuário inexistente e senha errada custam o mesmo tempo e
  devolvem a mesma mensagem.
- **Sem payload sensível em log** (ver [Observabilidade](#observabilidade)).
- **Validação de JWT sem folga:** issuer, audience, lifetime e assinatura, todos
  ligados. `ClockSkew` de 30 s em vez dos 5 minutos default — o default faz um token
  expirado continuar valendo por mais cinco minutos.
- **Nenhum segredo no repositório** — ver [A chave de assinatura](#a-chave-de-assinatura).
- **Fail fast em configuração ruim:** `JwtOptions` é validado com
  `ValidateDataAnnotations().ValidateOnStart()`, incluindo mínimo de 32 caracteres na
  chave (exigência do HS256). Um deploy sem chave derruba o container no boot em vez
  de subir emitindo tokens que não valem nada.
- **Container não roda como root** (`USER $APP_UID`).
- **`500` não vaza nada:** a exceção vai inteira para o log, a resposta não diz mais
  que "An unexpected error occurred.".

### A chave de assinatura

**Não existe chave de assinatura em nenhum arquivo deste repositório.**

As duas formas usuais de manter o "clonar e rodar" funcionando são piores do que
parecem: uma chave commitada em `appsettings.json` tem de ser tratada como
comprometida no instante em que é enviada, e um placeholder é exatamente o valor que
sobrevive até produção por acidente. Então o comportamento depende do ambiente:

| Ambiente          | Sem `Jwt__SigningKey` configurada                                 |
| ----------------- | ----------------------------------------------------------------- |
| `Development`     | Gera uma chave aleatória **por processo** e loga um `WRN`          |
| Qualquer outro    | **Não inicia** — `OptionsValidationException` nomeando a variável  |

Uma chave gerada por processo é estritamente melhor do que uma chave de
desenvolvimento compartilhada: nunca é escrita em disco, nunca é compartilhada entre
máquinas e não pode ser confundida com algo seguro de publicar. O único custo é que
reiniciar a aplicação invalida os tokens já emitidos — que é o comportamento desejado
localmente e inaceitável em produção, e é por isso que existe a checagem de ambiente
(`DevelopmentSigningKey` em `Program.cs`).

Consequência prática: `dotnet run` e `docker compose up` continuam funcionando sem
nenhuma configuração, e um deploy real é obrigado a fornecer a chave.

```bash
# fixar a chave localmente (sobrevive a restart, fora do controle de versão)
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"   --project src/Mazza.Orders.Api

# ou por variável de ambiente / .env (ver .env.example)
export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
docker compose up
```

A mensagem quando falta a chave fora de Development nomeia o que corrigir:

```
Microsoft.Extensions.Options.OptionsValidationException: DataAnnotation validation
failed for 'JwtOptions' members: 'SigningKey' with the error: 'Jwt:SigningKey is not
configured. Set the Jwt__SigningKey environment variable (or use dotnet user-secrets
/ a secret store). It must be at least 32 characters.'
```

Para isso valer, o `JwtBearerOptions` é configurado a partir de `IOptions<JwtOptions>`
em vez de uma segunda leitura independente do `IConfiguration`. Assim a chave que
valida o token é garantidamente a mesma que o assinou, e a leitura só acontece depois
da validação — lendo antes, um `string.Empty` chegaria ao `SymmetricSecurityKey` e o
erro de criptografia esconderia a mensagem que realmente diz o que fazer.

---

## O que eu faria diferente em produção

Fora do escopo deste teste, mas são as próximas coisas que eu faria:

1. **Refresh tokens e revogação.** Hoje é um access token de 60 minutos, sem
   revogação. O `jti` já está no token, que é o gancho para isso.
2. **Rate limiting no `/auth/login`.** Sem ele o endpoint aceita brute force à
   vontade; o .NET tem `AddRateLimiter` nativo.
3. **Migrations fora do startup.** Migrar no boot é certo para SQLite
   single-instance. Com várias réplicas contra um banco compartilhado virou corrida,
   e migration passa a ser passo de deploy (job/init container). O comentário em
   `DatabaseMigrationExtensions` registra isso.
4. **Concorrência otimista.** Dois `PATCH /cancel` simultâneos hoje podem ambos ler
   `Pending`. Uma coluna de versão no `Order` e `IsConcurrencyToken()` resolvem — não
   incluí porque adicionar sem um caso de uso concorrente real seria especulação.
5. **Value object `Money`.** `decimal` + `HasPrecision(18,2)` cobre bem enquanto há
   uma única moeda. Com mais de uma, o par (valor, moeda) precisa virar um tipo.
6. **CI.** `dotnet build` + `dotnet test` + scan do Sonar em cada PR — o
   `docker/sonar-scan.sh` já é o corpo desse job.
7. **PostgreSQL.** O SQLite foi pedido pelo enunciado. A troca é uma linha em
   `AddPersistence` mais um novo conjunto de migrations, exatamente porque nenhuma
   camada acima da Infrastructure conhece o provider.

---

## Stack

| Item              | Versão   | Nota                                            |
| ----------------- | -------- | ----------------------------------------------- |
| .NET              | 10.0     | Minimal API                                     |
| MediatR           | 12.5.0   | Última sob Apache-2.0                           |
| FluentValidation  | 12.1.1   | Com behavior no pipeline do MediatR             |
| EF Core           | 10.0.12  | Provider SQLite, migrations automáticas          |
| Serilog           | 10.0.0   | Sink de console, config em `appsettings.json`   |
| OpenTelemetry     | 1.18.0   | Traces + métricas, exporter de console          |
| xUnit             | 2.9.3    | 117 testes                                      |
| NSubstitute       | 6.2.0    | Substitutos nos testes unitários                |
| Scalar            | 2.17.3   | UI de OpenAPI (dev)                             |
