# 🏦 Homework 3: Virtual Credit Card

> **Student Name**: Vitalii Kulykivskyi
> **Date Submitted**: 2026-06-02
> **AI Tools Used**: [Claude Code (Model: Opus 4.8), Spec Kit, VS Code]

Homework 3 is an exercise in **specification-driven development**. Instead of jumping straight to
code, the goal was to use **[Spec Kit](https://github.com/github/spec-kit)** together with Claude
Code to turn a single natural-language product idea into a complete, reviewable engineering
specification — constitution, feature spec, clarifications, technical plan, design artifacts, and a
dependency-ordered task list — for a FinTech **Virtual Credit Card Management** platform.

## Summary of What Was Done

Starting from one prompt — *"Build a FinTech application that manages a virtual credit card…"* — the
following artifacts were produced under `specs/001-virtual-credit-card/`:

| Artifact | Purpose |
|----------|---------|
| `.specify/memory/constitution.md` | 7 governing principles (security, GDPR, auditability, quality, testing, UX, performance) — ratified as **v1.0.0** |
| `spec.md` | 8 prioritized user stories, 39 functional requirements, 12 measurable success criteria, edge cases, key entities |
| `checklists/requirements.md` | Spec-quality checklist used to validate completeness |
| `plan.md` | Technical plan: stack, architecture, constitution gate check, project structure |
| `research.md` | Phase 0 — technology decisions and rationale |
| `data-model.md` | Phase 1 — entities, relationships, money/ledger model |
| `contracts/schema.graphql` | GraphQL API contract (queries, mutations, subscriptions) |
| `contracts/events.md` | Kafka event/message contracts (audit, notifications, interest) |
| `contracts/kyc-integration.md` | Diia KYC integration contract |
| `quickstart.md` | Developer onboarding / local run guide |
| `tasks.md` | Phase 2 — dependency-ordered, story-grouped implementation tasks |

The product covers: Diia (Ukraine government ID) **KYC-gated registration**, virtual card lifecycle
(create / freeze / unfreeze), **company-suggested and personal credit limits**, payments,
deposits, withdrawals (with a 3% cash-advance fee on the credit-funded portion), filtered
transaction history, notifications, **daily deposit interest** on the minimum daily balance, and a
**60-day interest-free credit period** with post-grace daily credit interest.

## How the Specification Was Generated

The spec was built incrementally through the Spec Kit workflow, each step driven by a Claude Code
slash command and reviewed before moving on:

```mermaid
flowchart LR
    Idea["Product idea (one paragraph)"] --> C["/constitution<br/>govern principles"]
    C --> S["/specify<br/>spec.md"]
    S --> Cl["/clarify<br/>resolve ambiguities"]
    Cl --> P["/plan<br/>plan + research +<br/>data-model + contracts"]
    P --> T["/tasks<br/>tasks.md"]
    T --> Impl["/implement<br/>(next phase)"]

    C -. gates .-> P
    Cl -. updates .-> S

    classDef done fill:#d4edda,stroke:#28a745,color:#155724;
    classDef next fill:#fff3cd,stroke:#ffc107,color:#856404;
    class C,S,Cl,P,T done;
    class Impl next;
```

1. **`/constitution`** — established 7 non-negotiable/quality principles that act as a gate for all
   later design decisions.
2. **`/specify`** — converted the product idea into a structured, testable spec (user stories with
   priorities, functional requirements, success criteria) free of implementation detail.
3. **`/clarify`** — surfaced and resolved ambiguities through targeted questions; answers (e.g. *KYC
   blocks all financial functionality until it passes*) were encoded back into `spec.md`.
4. **`/plan`** — chose the technical approach and generated the design artifacts (`research.md`,
   `data-model.md`, `contracts/`), re-checking every constitution gate after design.
5. **`/tasks`** — derived a dependency-ordered, per-user-story task breakdown ready for
   implementation.

The constitution check is the key control: the plan documents how each of the 7 principles is
satisfied (e.g. immutable audit via transactional outbox + Debezium CDC, idempotency keys on every
money command, minimal KYC field storage) **before** any task is generated.

## Use-Case Diagram

The most common use cases, grouped by feature area, with the implied
`include` / `extend` relationships from the spec:

```mermaid
flowchart LR
    User(["Cardholder"])
    Diia(["Diia KYC (external)"])
    Compliance(["Compliance Reviewer"])
    Scheduler(["Daily Job (system)"])

    subgraph Onboarding["Registration and KYC (P1)"]
        UC1("Register account")
        UC2("Verify identity via Diia")
        UC3("Manual compliance review")
    end

    subgraph Card["Card and Credit Limits (P1)"]
        UC4("Create virtual card")
        UC5("Set personal credit limit")
        UC6("Freeze / unfreeze card")
    end

    subgraph Money["Money Movement (P2)"]
        UC7("Make payment")
        UC8("Deposit funds")
        UC9("Withdraw funds")
        UC10("Charge 3 percent cash-advance fee")
    end

    subgraph Visibility["Visibility and Earnings (P3)"]
        UC11("View / filter transactions")
        UC12("Receive notifications")
        UC13("Accrue daily interest")
    end

    User --> UC1
    User --> UC4
    User --> UC5
    User --> UC6
    User --> UC7
    User --> UC8
    User --> UC9
    User --> UC11

    UC2 --- Diia
    UC3 --- Compliance
    UC13 --- Scheduler
    UC12 -.notifies.-> User

    UC1 -. include .-> UC2
    UC2 -. extend .-> UC3
    UC4 -. requires verified .-> UC2
    UC4 -. include .-> UC5
    UC7 -. requires active card .-> UC6
    UC9 -. extend credit-funded .-> UC10
    UC7 -. trigger .-> UC12
    UC8 -. trigger .-> UC12
    UC9 -. trigger .-> UC12
```

| Use case | Story / FRs |
|---|---|
| Register → verify via Diia (`include`) | US1 · FR-031–039 |
| Diia failure → manual review (`extend`) | US1 · FR-036 |
| Create card requires passed KYC | US2 · FR-001, FR-033 |
| Set personal limit (≤ company max) | US2 · FR-004–008 |
| Make payment (active card, within capacity) | US3 · FR-009–011 |
| Deposit / withdraw; credit-funded withdrawal → 3% fee | US4 · FR-012–016 |
| Freeze / unfreeze | US5 · FR-002–003 |
| View / filter transactions | US6 · FR-017–019 |
| Notifications (triggered by changes & failures) | US7 · FR-020–022 |
| Daily interest (system-driven) | US8 · FR-023–026 |

## Architecture

The plan specifies a **.NET 10 modular monolith** organized by DDD bounded contexts, a HotChocolate
**GraphQL** API, **PostgreSQL** as the system of record, and a fully decoupled audit/event pipeline
over **Kafka** (fed by a transactional **outbox** via **Debezium** CDC). The client is a **.NET MAUI
Hybrid** app. Everything is hosted on **Azure**.

```mermaid
flowchart TB
    subgraph Client[".NET MAUI Hybrid Client"]
        UI["MVVM feature modules<br/>Identity, Cards, Money,<br/>Transactions, Notifications"]
        SS["StrawberryShake<br/>GraphQL client"]
        LC[("SQLite + SQLCipher<br/>encrypted cache")]
        UI --> SS
        UI --> LC
    end

    subgraph Backend["Modular Monolith Backend (Azure)"]
        API["HotChocolate GraphQL API<br/>authN/Z, field authorization"]
        App["Application: CQRS / MediatR<br/>LanguageExt Fin/Validation"]
        subgraph Domain["Domain (DDD bounded contexts)"]
            D1["Identity / KYC"]
            D2["Cards"]
            D3["Ledger"]
            D4["Interest"]
        end
        Infra["Infrastructure<br/>EF Core, Npgsql, Outbox"]
        API --> App --> Domain
        App --> Infra
    end

    PG[("PostgreSQL<br/>system of record<br/>plus Outbox table")]
    Debezium["Debezium CDC"]
    Kafka{{"Apache Kafka"}}
    Audit[("Append-only<br/>audit store")]
    Workers["Event consumers<br/>Notifications, Interest postings"]

    Diia["Diia KYC (external)"]
    OIDC["OIDC Provider<br/>IdentityModel.OidcClient"]
    Logs[("Serilog to<br/>Azure Log Analytics")]

    SS -- "HTTPS / GraphQL" --> API
    UI -. "OIDC / PKCE" .-> OIDC
    API -. authenticate .-> OIDC

    Infra --> PG
    Infra -- IKycGateway --> Diia
    Backend --> Logs

    PG -- WAL --> Debezium --> Kafka
    Kafka --> Audit
    Kafka --> Workers
    Workers -. push .-> Client

    classDef ext fill:#f8d7da,stroke:#dc3545,color:#721c24;
    classDef store fill:#e2e3e5,stroke:#6c757d,color:#383d41;
    class Diia,OIDC ext;
    class PG,Audit,LC,Logs store;
```

**Key architectural decisions** (from `plan.md`):

- **Modular monolith over microservices** — cheaper to operate at ~10k-user scale while preserving
  DDD boundaries and a clean extraction path. Modules talk in-process via MediatR and
  asynchronously via Kafka.
- **Functional error handling** — LanguageExt `Fin<T>` / `Validation` instead of exceptions for
  control flow.
- **Decoupled auditability** — a transactional outbox + Debezium CDC streams every finance/security
  action into an append-only audit store, satisfying the immutable-audit principle.
- **Idempotency everywhere** — money commands carry idempotency keys so retries never double-process.
- **GDPR data minimization** — only the KYC result, a Diia reference, and minimal identifiers are
  stored (no document images).

## Rationale

**Why this spec is structured the way it is.** The homework asks for a *layered, traceable*
specification (see `TASKS.md`). The Spec Kit format delivers exactly that layering: a north-star
plus scope boundary (`spec.md` → *Feature Specification* / *Assumptions*), observable mid-level
outcomes (`spec.md` → *User Scenarios*, 8 prioritized stories), non-functional/policy guardrails
(`.specify/memory/constitution.md` + `spec.md` → *FR-027–030* cross-cutting requirements),
implementation notes (`plan.md` → *Technical Context* / *Constitution Check*; `research.md`), and a
genuinely decomposed low-level task list (`tasks.md`). Requirements stay traceable end-to-end: every
task in `tasks.md` is tagged with the user story (US1–US8) it serves, every story maps to functional
requirements (FR-xxx) in `spec.md`, and every requirement rolls up to a measurable success criterion
(SC-xxx). The use-case and architecture diagrams above make that mapping legible at a glance.

**Why prioritized, independently testable stories.** Stories are ranked P1–P3 (`spec.md` → *User
Scenarios*) so the foundational, compliance-critical slices (KYC, card creation) precede
convenience features (freeze, history, notifications). Each story carries its own *Independent Test*
and *Acceptance Scenarios*, so the spec doubles as a verification plan rather than leaving testing
as an afterthought.

**How performance targets were chosen.** The seed brief gave no numbers, so targets were *derived*
rather than invented, and explicitly labeled as assumptions for stakeholder confirmation
(`plan.md` → *Performance Goals*; `research.md` → *Resolved unknowns*). Two sources drove them:

- **Reverse-engineered from the spec's own success criteria** — these are user-observable promises,
  not arbitrary SLAs: card creation end-to-end `< 3 min` (SC-002), freeze/unfreeze effective for new
  payments `< 5 s` (SC-004), notifications delivered `< 1 min` (SC-007), and daily interest
  completing within its processing window (SC-008).
- **FinTech UX/ops norms for the rest** — GraphQL read p95 `< 300 ms` and mutation p95 `< 500 ms`
  reflect typical interactive-banking responsiveness; the `~10k users / ~50 mutations/s` scale
  (`plan.md` → *Scale/Scope*) is a conservative starting envelope chosen so the modular-monolith
  decision is defensible. Each is a target/range, not a vague "fast," and each is flagged as an
  assumed default.

**How verification depth was chosen.** Depth is driven by *risk*, governed by the constitution
rather than by taste. Principle V (*Testing Standards — NON-NEGOTIABLE*) mandates automated coverage
of critical paths, so `tasks.md` deliberately includes test tasks *even though the spec did not
request them* (see its header note) and concentrates them on money movement, limits, KYC/auth, and
interest math — the paths where a defect is most costly. Verification appears at three levels:
acceptance scenarios per story (`spec.md`), measurable success criteria with explicit 100% targets
for money/audit/idempotency (`spec.md` → *Success Criteria*, SC-001/003/005/010/011/012), and
contract/integration test tasks against the GraphQL schema and the outbox→audit pipeline
(`tasks.md`; `contracts/`). Lower-risk surfaces (e.g. transaction-history filtering) get lighter
coverage by design.

## Industry Best Practices

FinTech/regulated-environment practices were added deliberately and live **inside the spec
artifacts** (not just this README), so an implementer or agent cannot miss them:

| Practice | Where it appears |
|---|---|
| **KYC / AML gating** — registration blocked until identity verification passes | `spec.md` → US1, FR-031–039, SC-001; `contracts/kyc-integration.md`; `research.md` §9 |
| **GDPR data minimization & retention** — store only KYC result + reference + minimal identifiers; defined retention; data-subject rights | `spec.md` → FR-038, FR-030; `.specify/memory/constitution.md` → Principle II; `data-model.md`; `plan.md` → Constitution Check |
| **Immutable, complete audit trail** — every finance/security action recorded with who/what/when/context | `spec.md` → FR-028, SC-011; `.specify/memory/constitution.md` → Principle III; `research.md` §5; `contracts/events.md` |
| **Idempotency on money operations** — retries never double-process | `spec.md` → FR-029, SC-012, *Edge Cases*; `research.md` §2, §10; `plan.md` → Constraints |
| **Least-privilege authN/Z** — users act only on their own cards; deny-by-default | `spec.md` → FR-027; `.specify/memory/constitution.md` → Principle I; `research.md` §3, §8 |
| **Encryption in transit & at rest** — TLS 1.2+, Postgres + SQLCipher on device, secrets in Key Vault | `plan.md` → Constraints / Constitution Check; `.specify/memory/constitution.md` → Principle I; `research.md` §4, §11 |
| **Sensitive-data hygiene** — never expose PAN/credentials in notifications, logs, or errors | `spec.md` → FR-022; `research.md` §2, §7; `.specify/memory/constitution.md` → Principle I |
| **Money correctness** — `numeric(19,4)`, optimistic concurrency, minimum-daily-balance interest | `research.md` §4, §10; `data-model.md`; `spec.md` → FR-023, *Edge Cases* |
| **Explicit edge cases & failure modes** — KYC outage, concurrency, duplicate ops, limit-below-balance | `spec.md` → *Edge Cases*; `research.md` §9 (circuit breaker / retry-later) |
| **Graceful degradation** — retry/backoff + circuit breaking on external calls (Diia, Kafka) | `plan.md` → Constitution Check (Principle VII); `research.md` §9 |
| **Observability & breach detection** — structured logs, correlation IDs, 72-hour breach window | `research.md` §7; `plan.md` → Security & Compliance Requirements |
| **Accessible, consistent UX** — WCAG 2.1 AA, centralized money/date formatting, no dark patterns | `.specify/memory/constitution.md` → Principle VI; `plan.md` → Constitution Check |

The **constitution gate** is what binds these practices to the design: `plan.md` → *Constitution
Check* shows, principle by principle, how the design satisfies each one before any task is generated,
and re-checks them after Phase 1.

## Repository Layout

```text
homework-3/
├── .specify/
│   ├── memory/constitution.md        # Project constitution (v1.0.0)
│   └── templates/                    # Spec Kit templates
├── specs/001-virtual-credit-card/
│   ├── spec.md                       # Feature specification
│   ├── plan.md                       # Technical implementation plan
│   ├── research.md                   # Phase 0 — tech decisions
│   ├── data-model.md                 # Phase 1 — entities & relationships
│   ├── quickstart.md                 # Developer onboarding
│   ├── tasks.md                      # Phase 2 — implementation tasks
│   ├── checklists/requirements.md    # Spec-quality checklist
│   └── contracts/                    # GraphQL schema, Kafka events, KYC integration
└── CLAUDE.md                         # Project context for Claude Code
```

## How to Reproduce the Spec Workflow

Within Claude Code (with Spec Kit installed), the spec was produced by running these commands in
order:

```text
/speckit-constitution   # ratify governing principles
/speckit-specify        # generate spec.md from the product idea
/speckit-clarify        # resolve ambiguities, encode answers back into the spec
/speckit-plan           # produce plan.md + research/data-model/contracts
/speckit-tasks          # generate dependency-ordered tasks.md
```

The next phase, `/speckit-implement`, would execute `tasks.md` to build the modular monolith and
MAUI client described above.
