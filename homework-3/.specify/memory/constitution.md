<!--
SYNC IMPACT REPORT
==================
Version change: 1.0.0 → 1.1.0
Bump rationale: Materially expanded two existing principles with new mandatory rules
  (Clean Architecture + async naming under IV; red-green TDD under V). Additive,
  backward-compatible principle expansion → MINOR.

Amendment 1.1.0 (2026-06-06):
- IV. Code Quality & Maintainability — added Clean Architecture dependency-direction rule
  and a ban on the `Async` method-name suffix.
- V. Testing Standards — added a red-green-refactor (TDD) mandate (test-first).
- Dependent templates: no edits required (gates are principle-driven and self-populating).

-- Original 1.0.0 ratification report below --

Version change: (template / unversioned) → 1.0.0
Bump rationale: Initial ratification of a concrete project constitution from the
  placeholder template. All principles defined for the first time → MAJOR baseline 1.0.0.

Modified principles (placeholder → defined):
- [PRINCIPLE_1_NAME] → I. Security by Design (NON-NEGOTIABLE)
- [PRINCIPLE_2_NAME] → II. Data Protection & GDPR Compliance (NON-NEGOTIABLE)
- [PRINCIPLE_3_NAME] → III. Auditability & Traceability (NON-NEGOTIABLE)
- [PRINCIPLE_4_NAME] → IV. Code Quality & Maintainability
- [PRINCIPLE_5_NAME] → V. Testing Standards (NON-NEGOTIABLE)
- (added) → VI. User Experience Consistency
- (added) → VII. Performance & Reliability

Added sections:
- Security & Compliance Requirements (was [SECTION_2_NAME])
- Development Workflow & Quality Gates (was [SECTION_3_NAME])

Removed sections: none

Templates requiring updates:
- ✅ .specify/templates/plan-template.md — Constitution Check gate aligns with these principles (no edit required; gate is principle-driven and self-populating)
- ✅ .specify/templates/spec-template.md — security/privacy/audit live as functional requirements & success criteria (compatible; no edit required)
- ✅ .specify/templates/tasks-template.md — already includes security hardening + cross-cutting phases; principle-driven task types covered (no edit required)
- ⚠ README.md / docs/quickstart.md — not present in repo; no runtime guidance docs to update

Follow-up TODOs: none
-->

# FinTech Platform Constitution

## Core Principles

### I. Security by Design (NON-NEGOTIABLE)

Security is the foundation of every feature, not an afterthought. The following rules are
non-negotiable:

- All data in transit MUST use TLS 1.2 or higher; all sensitive data at rest MUST be
  encrypted using industry-standard algorithms (AES-256 or stronger).
- Authentication MUST be enforced on every non-public endpoint; authorization MUST follow
  least-privilege and deny-by-default. No endpoint ships without an explicit access decision.
- Secrets (keys, tokens, credentials) MUST NEVER be committed to source control, logged, or
  exposed in error messages. They MUST be sourced from a managed secret store.
- All external input MUST be validated and sanitized; outputs MUST be encoded to prevent
  injection (SQLi, XSS, command injection).
- Every change MUST pass dependency vulnerability scanning and static security analysis (SAST)
  before merge. Known high/critical vulnerabilities block release.

**Rationale**: In the FinTech domain a single breach can cause irreversible financial loss,
regulatory penalties, and loss of trust. Security cannot be retrofitted.

### II. Data Protection & GDPR Compliance (NON-NEGOTIABLE)

Personal and financial data MUST be handled in accordance with GDPR and applicable financial
regulations:

- Data collection MUST follow data minimization: collect only what is necessary for the stated,
  lawful purpose, and document the lawful basis for each personal-data field.
- Personal data MUST be classified (e.g., PII, financial, special category) and protected
  according to its classification. Pseudonymization or encryption MUST be applied where feasible.
- Data subject rights MUST be supported: access, rectification, erasure ("right to be
  forgotten"), portability, and restriction. Each MUST be technically actionable, not manual-only.
- Retention periods MUST be defined per data category; data MUST be deleted or anonymized when
  the period expires. No indefinite retention of personal data.
- Cross-border data transfers and third-party processors MUST be documented with an appropriate
  legal safeguard. Consent, where it is the lawful basis, MUST be explicit, recorded, and
  revocable.

**Rationale**: GDPR non-compliance carries fines up to 4% of global turnover and erodes user
trust. Privacy obligations must be enforceable in code, not just policy documents.

### III. Auditability & Traceability (NON-NEGOTIABLE)

Every security- and finance-relevant action MUST be reconstructable after the fact:

- All access to and mutation of personal or financial data MUST emit an immutable, tamper-evident
  audit log entry capturing who, what, when, and the originating context (request/trace ID).
- Audit logs MUST NOT contain sensitive payloads (e.g., full card numbers, passwords); sensitive
  values MUST be masked or referenced by token.
- Authentication events, authorization denials, and configuration/permission changes MUST be
  logged.
- Audit logs MUST be retained per regulatory requirement and protected from modification or
  deletion by application-level actors.
- A distributed correlation/trace ID MUST flow across services so any transaction can be traced
  end to end.

**Rationale**: Financial regulators and incident responders require a verifiable record. Audit
trails are also the primary evidence in dispute resolution and fraud investigation.

### IV. Code Quality & Maintainability

Code MUST be clear, reviewed, and consistently styled:

- Clean Architecture principles MUST be followed: source dependencies point inward toward the
  domain; domain and application logic MUST NOT depend on frameworks, UI, persistence, or other
  infrastructure; layer/boundary contracts MUST be respected and crossed only through abstractions.
- Asynchronous methods MUST NOT use the `Async` suffix in their names.
- Every change MUST be submitted via pull request and approved by at least one other engineer
  before merge. Changes touching auth, payments, or personal data require domain-owner review.
- Automated linting and formatting MUST pass in CI; style is enforced by tooling, not debate.
- Functions and modules MUST have a single clear responsibility; complexity that cannot be
  removed MUST be justified in the PR and tracked.
- Public interfaces MUST be documented; non-obvious decisions MUST be explained in code or PR.
- Dead code, commented-out blocks, and TODOs without an owner/ticket MUST NOT be merged.

**Rationale**: FinTech systems are long-lived and high-stakes; readable, reviewed code reduces
defect rates and lowers the cost of regulatory and security changes.

### V. Testing Standards (NON-NEGOTIABLE)

Correctness MUST be demonstrated by automated tests:

- A red-green-refactor (TDD) approach MUST be followed: write a failing test that specifies the
  desired behavior (red), implement the minimum needed to pass it (green), then refactor while
  keeping tests green. Tests precede implementation, not follow it.
- Critical paths — authentication, authorization, money movement, and personal-data handling —
  MUST have automated test coverage before merge.
- New or changed contracts between services MUST have contract/integration tests.
- Tests MUST be deterministic and isolated; flaky tests MUST be fixed or quarantined, never
  ignored.
- A change that fixes a bug MUST include a regression test reproducing that bug.
- CI MUST run the full automated test suite on every change; a red suite blocks merge and release.

**Rationale**: In a domain handling real money and regulated data, undetected regressions are
unacceptable. Tests are the executable specification of correct behavior.

### VI. User Experience Consistency

The product MUST behave predictably and accessibly across all surfaces:

- Shared interaction patterns, terminology, and components MUST be reused; teams MUST NOT
  reinvent flows that already exist.
- Error states MUST be clear, actionable, and MUST NOT leak sensitive or internal detail to the
  user.
- Financial figures, dates, currencies, and time zones MUST be formatted consistently and
  unambiguously across the product.
- Interfaces MUST meet WCAG 2.1 AA accessibility as a baseline.
- Security and consent interactions (e.g., authentication, data-sharing prompts) MUST be
  consistent and understandable, never dark-patterned.

**Rationale**: Consistency builds the trust essential to financial products and reduces user
error in operations where mistakes have monetary consequences.

### VII. Performance & Reliability

The system MUST meet defined performance and availability targets:

- Each user-facing service MUST define explicit latency targets (e.g., p95/p99) and MUST be
  measured against them; regressions block release.
- Throughput and resource budgets MUST be defined for critical transaction paths and validated
  under representative load before launch.
- The system MUST degrade gracefully under failure (timeouts, retries with backoff, circuit
  breaking) and MUST avoid double-processing of financial transactions (idempotency required).
- Capacity and scaling assumptions MUST be documented; performance-relevant changes MUST include
  before/after evidence.

**Rationale**: Latency and downtime in financial transactions cause direct revenue loss, failed
settlements, and erosion of trust. Performance targets must be explicit and verified.

## Security & Compliance Requirements

These constraints apply across all features and supplement the principles above:

- **Regulatory baseline**: GDPR is mandatory for all personal data; where payment card data is
  handled, PCI DSS controls apply. Applicable local financial regulations take precedence where
  stricter.
- **Threat modeling**: Features that handle money, credentials, or personal data MUST include a
  lightweight threat-model review during planning.
- **Data lifecycle**: Every personal-data store MUST document classification, lawful basis,
  retention period, and deletion mechanism before it goes to production.
- **Incident readiness**: Logging and alerting MUST be sufficient to detect and report a personal
  data breach within the GDPR 72-hour notification window.
- **Third parties**: New external dependencies and data processors MUST be reviewed for security
  posture and data-processing agreements before adoption.

## Development Workflow & Quality Gates

All work MUST pass these gates before reaching production:

1. **Planning gate**: Every feature plan MUST complete the Constitution Check, identifying
   security, privacy (GDPR), and audit impacts, and MUST justify any complexity or principle
   deviation.
2. **Review gate**: At least one peer approval; mandatory domain-owner approval for changes to
   authentication, authorization, payments, or personal-data handling.
3. **Automated gate (CI)**: Linting, formatting, full test suite, SAST, and dependency
   vulnerability scanning MUST all pass. Any failure blocks merge.
4. **Pre-release gate**: Performance targets validated for affected critical paths; audit logging
   verified for new sensitive operations; data-lifecycle documentation present for new personal
   data.
5. **Traceability**: Every change MUST be linked to its specification/task, preserving the
   spec → plan → tasks → implementation chain.

## Governance

- This constitution supersedes other development practices. Where a conflict exists, the
  constitution wins and the conflicting practice MUST be corrected.
- **Amendment procedure**: Changes to this constitution MUST be proposed via pull request,
  include the rationale and a migration/impact note, and be approved by the project's technical
  and compliance owners. Upon merge, dependent templates and guidance MUST be re-synced.
- **Versioning policy**: This document follows semantic versioning. MAJOR for backward-
  incompatible governance or principle removals/redefinitions; MINOR for newly added or
  materially expanded principles/sections; PATCH for clarifications and non-semantic refinements.
- **Compliance review**: Adherence MUST be verified at each quality gate above. NON-NEGOTIABLE
  principles (I, II, III, V) MUST NOT be waived; other deviations MUST be documented and approved
  in the feature's Complexity Tracking with justification.
- **Periodic review**: This constitution SHOULD be reviewed at least annually, and after any
  significant security incident or regulatory change, to confirm it remains accurate and adequate.

**Version**: 1.1.0 | **Ratified**: 2026-05-30 | **Last Amended**: 2026-06-06
