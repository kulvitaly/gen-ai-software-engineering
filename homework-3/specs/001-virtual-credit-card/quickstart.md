# Quickstart: Virtual Credit Card Management

Developer setup for the .NET 10 backend (modular monolith + GraphQL) and the MAUI Hybrid client.

## Prerequisites

- .NET 10 SDK
- Docker Desktop (PostgreSQL, Kafka, Debezium via Compose / Testcontainers)
- MAUI workloads: `dotnet workload install maui`
- (Optional) Azure CLI for deploying to Azure

## Local infrastructure

Bring up PostgreSQL, Kafka, and Debezium locally:

```bash
docker compose -f deploy/local/docker-compose.yml up -d   # postgres, kafka, kafka-connect(debezium)
```

Register the Debezium connector (captures the `outbox` table → Kafka):

```bash
curl -X POST http://localhost:8083/connectors -H "Content-Type: application/json" \
  -d @deploy/local/debezium-outbox-connector.json
```

## Backend

```bash
cd backend
dotnet restore
dotnet ef database update -p src/VirtualCard.Infrastructure -s src/VirtualCard.Api   # migrations
dotnet run --project src/VirtualCard.Api                                             # GraphQL at /graphql
```

Configuration (user-secrets / Key Vault in Azure):
- `ConnectionStrings:Postgres`
- `Kafka:BootstrapServers`
- `Oidc:Authority`, `Oidc:Audience`
- `Diia:BaseUrl`, `Diia:ApiKey`
- `Interest:DepositDailyRate`, `Interest:CreditApr`, `Fees:CashAdvancePercent` (= 0.03)
- `Serilog:AzureLogAnalytics:WorkspaceId/AuthenticationId`

Open the Banana Cake Pop IDE at `https://localhost:5001/graphql` to explore the schema.

## Frontend (MAUI Hybrid)

```bash
cd frontend
dotnet restore
# Generate the StrawberryShake client from the API schema:
dotnet graphql update -p src/VirtualCard.Mobile.GraphQL    # pulls schema.graphql
dotnet build src/VirtualCard.Mobile.App -t:Run -f net10.0-android   # or -ios / -windows
```

Client configuration: OIDC authority/client id (IdentityModel.OidcClient), GraphQL endpoint,
and a SQLCipher passphrase sourced from platform secure storage (never hard-coded).

## Tests

```bash
cd backend
dotnet test                      # domain + application (fast), integration (Testcontainers)
```

Integration tests spin up PostgreSQL and Kafka via Testcontainers and assert the
outbox → Debezium → audit path and money invariants.

## Smoke test (maps to user stories)

1. **Register + KYC** (US1): `register` → `submitKyc` (fake gateway `Passed`) → account `ACTIVE`;
   verify financial mutations are blocked while `PENDING_VERIFICATION`.
2. **Create card + limit** (US2): `createCard` → `setPersonalLimit` (≤ company max) → effective
   limit reflects the lower value.
3. **Payment** (US3): `makePayment` within capacity posts; over-capacity / frozen declines.
4. **Deposit/Withdraw** (US4): deposit increases balance; withdrawal beyond positive balance draws
   credit with a paired 3% fee; beyond effective limit declines.
5. **Freeze/Unfreeze** (US5): freeze blocks payments; unfreeze restores.
6. **Transactions** (US6): `transactions(filter:)` by date/type/amount/status.
7. **Notifications** (US7): subscribe to `notificationReceived`; trigger an account change.
8. **Interest** (US8): run the daily interest job; verify minimum-daily-balance deposit interest
   and post-60-day credit interest postings (idempotent on re-run).
