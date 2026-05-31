# Feature Specification: Virtual Credit Card Management

**Feature Branch**: `001-virtual-credit-card`

**Created**: 2026-05-30

**Status**: Draft

**Input**: User description: "Build a FinTech application that manages virtual credit card. User can create/freeze/unfreeze card. User can use the card for payments. User can deposit or withdraw money from the card. Based on user's credit rating the company can suggest an appropriate credit limit. Also user can set personal credit limits (that are lower than max credit limit offered by the company). User can view all transactions, based on specified filters. Also user should receive notifications on account changes or failed operations. User has 60 days credit time without any additional charges. If user has positive balance on his card - that money are treated as deposit and interest income is calculated and paid on a daily basis."

## Clarifications

### Session 2026-05-31

- Q: Must a user complete Diia KYC before using any financial functionality? → A: Block until KYC
  passes — registration creates a "pending verification" account; card creation and all money
  movement are blocked until KYC succeeds.
- Q: What happens when Diia KYC fails, is rejected, or Diia is unavailable? → A: Account stays
  unverified and blocked; user may retry; repeated/ambiguous failures route to manual compliance
  review; a Diia outage shows a retry-later state without activating the account.
- Q: What identity data from Diia should be stored (GDPR data minimization)? → A: Verification
  result + a Diia verification reference + minimal identifiers (full name, date of birth, national
  tax ID / RNOKPP reference); no document images.
- Q: Is KYC one-time or an ongoing AML obligation? → A: One-time at registration — no periodic
  re-verification or ongoing screening in this release.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register and verify identity via Diia KYC (Priority: P1)

A new user registers for an account. As part of registration the user MUST complete a Know Your
Customer (KYC) identity verification through Diia (Ukraine's government digital-identity
smartphone app). The account remains in a "pending verification" state and no financial
functionality is available until KYC passes. Successful verification activates the account; on
failure or Diia unavailability the user may retry, and edge cases route to manual compliance
review.

**Why this priority**: KYC is a legal anti-money-laundering (AML) prerequisite and the entry
point to the product. Without a registered, verified user, no card or money movement is permitted.
This is the most foundational slice.

**Independent Test**: Register a new user, complete a Diia KYC flow that passes, and confirm the
account becomes active and able to proceed to card creation; separately, simulate a KYC failure
and a Diia outage and confirm the account stays blocked with retry and manual-review paths.

**Acceptance Scenarios**:

1. **Given** a new visitor, **When** they register and successfully complete Diia KYC, **Then** the
   account becomes active and the user may proceed to create a card.
2. **Given** a registered user whose KYC has not yet passed, **When** they attempt any financial
   operation (create card, pay, deposit, withdraw), **Then** the operation is blocked with a clear
   "verification required" message.
3. **Given** a Diia KYC check that fails or is rejected, **When** the result is returned, **Then**
   the account remains unverified and blocked, the user is notified, and they may retry.
4. **Given** repeated KYC failures or an ambiguous result, **When** retries are exhausted or
   flagged, **Then** the case is routed to manual compliance review rather than auto-rejected.
5. **Given** Diia is temporarily unavailable, **When** the user attempts KYC, **Then** the system
   shows a retry-later state without losing the registration, and does not activate the account.
6. **Given** a successful KYC, **When** verification data is stored, **Then** only the verification
   result, a Diia verification reference, and minimal identifiers (full name, date of birth,
   national tax ID / RNOKPP reference) are retained — not document images.

---

### User Story 2 - Create and configure a virtual credit card (Priority: P1)

A verified user requests a new virtual credit card. The company evaluates the user's credit
rating and offers a maximum credit limit. The user accepts the card and may set a personal
credit limit that is lower than the company-offered maximum.

**Why this priority**: Without a provisioned card and an agreed credit limit, no other capability
(payments, deposits, interest) can exist. This is the foundational MVP slice.

**Independent Test**: Create a card for a user with a known credit rating, confirm a company
maximum limit is offered, set a personal limit below the maximum, and confirm the card is
active and usable with the chosen limit.

**Acceptance Scenarios**:

1. **Given** a verified user with an eligible credit rating, **When** they request a new card,
   **Then** the system creates an active virtual card and presents a company-suggested maximum
   credit limit derived from the credit rating.
2. **Given** a newly offered card with a maximum limit, **When** the user sets a personal limit
   lower than the maximum, **Then** the system applies the personal limit as the effective
   spending limit.
3. **Given** a card with a company maximum limit, **When** the user attempts to set a personal
   limit higher than the maximum, **Then** the system rejects the change and explains the
   allowed range.
4. **Given** a user whose credit rating makes them ineligible, **When** they request a card,
   **Then** the system declines the request with a clear, non-sensitive reason.

---

### User Story 3 - Make payments with the card (Priority: P2)

The cardholder uses the active virtual card to pay a merchant. The payment is authorized only
when the card is active and the amount fits within the available spending capacity (positive
balance plus remaining credit, up to the effective limit).

**Why this priority**: Spending is the primary purpose of a credit card and the main source of
user value once a card exists.

**Independent Test**: With an active card and a known available capacity, submit a payment within
capacity (expect approval) and a payment exceeding capacity (expect decline), then confirm the
balance and available capacity update correctly.

**Acceptance Scenarios**:

1. **Given** an active card with sufficient available capacity, **When** a payment is submitted,
   **Then** the payment is approved, the balance is updated, and a transaction record is created.
2. **Given** an active card with insufficient available capacity, **When** a payment is
   submitted, **Then** the payment is declined and the user is notified of the failed operation.
3. **Given** a frozen card, **When** a payment is submitted, **Then** the payment is declined
   because the card is not active.
4. **Given** a payment that would exceed the effective credit limit, **When** it is submitted,
   **Then** it is declined without partially charging the user.

---

### User Story 4 - Deposit and withdraw funds (Priority: P2)

The cardholder adds money to the card (deposit), creating or increasing a positive balance, or
takes money out (withdrawal). A withdrawal first draws on the available positive balance; any
portion beyond the positive balance is a cash advance against the credit line (up to the
effective limit) and incurs a 3% cash-advance fee on the credit-funded portion.

**Why this priority**: Deposits enable the interest-earning behavior and reduce credit usage;
withdrawals (including cash advances) give users access to their funds and credit. Both are core
money-movement operations.

**Independent Test**: Deposit a known amount and confirm the positive balance increases; withdraw
an amount within the positive balance and confirm it decreases with no fee; withdraw an amount
that exceeds the positive balance and confirm the excess is drawn as credit with a 3% fee on that
credit portion; attempt a withdrawal beyond the effective limit and confirm it is declined.

**Acceptance Scenarios**:

1. **Given** an active card, **When** the user deposits funds, **Then** the positive balance
   increases by the deposited amount and a transaction record is created.
2. **Given** a card with a positive balance, **When** the user withdraws an amount within that
   balance, **Then** the positive balance decreases, no cash-advance fee is charged, and a
   transaction record is created.
3. **Given** a card whose available capacity includes credit, **When** the user withdraws an
   amount exceeding the positive balance but within the effective limit, **Then** the excess is
   recorded as used credit, a 3% fee on the credit-funded portion is charged as a fee transaction
   at withdrawal time, and the user is notified.
4. **Given** a card, **When** the user attempts to withdraw more than the available capacity
   (positive balance plus remaining credit up to the effective limit), **Then** the withdrawal is
   declined and the user is notified.

---

### User Story 5 - Freeze and unfreeze the card (Priority: P3)

The cardholder temporarily disables the card (freeze) to block new payments, then re-enables it
(unfreeze) when ready to use it again.

**Why this priority**: An important security and control feature, but the product is usable
without it for an initial release.

**Independent Test**: Freeze an active card and confirm payments are blocked; unfreeze it and
confirm payments are allowed again.

**Acceptance Scenarios**:

1. **Given** an active card, **When** the user freezes it, **Then** the card state becomes frozen
   and subsequent payment attempts are declined.
2. **Given** a frozen card, **When** the user unfreezes it, **Then** the card state becomes active
   and payments are allowed again.
3. **Given** a card state change (freeze or unfreeze), **When** it completes, **Then** the user
   receives a notification of the account change.

---

### User Story 6 - View and filter transaction history (Priority: P3)

The cardholder reviews their transactions and narrows the list using filters such as date range,
transaction type (payment, deposit, withdrawal, interest, fee), amount range, and status.

**Why this priority**: Transparency and record-keeping are valuable, but depend on transactions
already being generated by higher-priority stories.

**Independent Test**: Generate transactions of several types, apply each filter individually and
in combination, and confirm only matching transactions are returned.

**Acceptance Scenarios**:

1. **Given** a history with multiple transaction types, **When** the user filters by a date
   range, **Then** only transactions within that range are shown.
2. **Given** a transaction history, **When** the user filters by transaction type and status,
   **Then** only matching transactions are shown.
3. **Given** no transactions match the selected filters, **When** the filter is applied, **Then**
   the system shows an empty result with a clear message rather than an error.

---

### User Story 7 - Receive notifications (Priority: P3)

The cardholder is notified about account changes (e.g., card created, frozen, unfrozen, limit
changed, deposit/withdrawal posted, interest paid) and about failed operations (e.g., declined
payment, rejected withdrawal).

**Why this priority**: Notifications improve trust and awareness but are not required for the
core money-movement loop to function.

**Independent Test**: Trigger each notable account change and each failed-operation case, and
confirm a corresponding notification is generated for the user.

**Acceptance Scenarios**:

1. **Given** an account change occurs, **When** it is committed, **Then** the user receives a
   notification describing the change without exposing sensitive data.
2. **Given** an operation fails (e.g., declined payment), **When** the failure is recorded, **Then**
   the user receives a notification explaining the failure in non-sensitive terms.

---

### User Story 8 - Earn daily interest and 60-day interest-free credit (Priority: P3)

When the card holds a positive balance, that balance is treated as a deposit and earns interest
calculated and paid on a daily basis at a fixed company-configured rate. Each day's deposit
interest is computed on the **minimum balance held during that day** (the lowest balance the card
reached that day). When the card carries outstanding used credit (negative balance), the user has
a 60-day interest-free period during which no additional charges apply; once that period elapses
with credit still outstanding, the outstanding credit accrues daily interest at a fixed
company-configured APR (using the same minimum-daily-balance basis) until repaid.

**Why this priority**: This differentiating financial behavior depends on balances created by the
payment and deposit stories, so it is built once those foundations exist.

**Independent Test**: Hold a positive balance over several days and confirm daily interest is
accrued on each day's minimum balance and paid as transactions; carry used credit and confirm no
charges accrue within 60 days, then confirm daily interest begins on day 61 if still outstanding.

**Acceptance Scenarios**:

1. **Given** a card with a positive balance, **When** a day closes, **Then** interest for that day
   is calculated on the minimum balance held during that day at the fixed rate and paid as an
   interest transaction.
2. **Given** a card that starts a day at 100 and is reduced to 80 by a payment during the day,
   **When** the day closes, **Then** that day's interest is calculated on 80 (the day's minimum).
3. **Given** a card that starts a day at 100 and receives a 50 deposit during the day, **When** the
   day closes, **Then** that day's interest is calculated on 100 (the day's minimum, since the
   balance never dropped below 100).
4. **Given** a card carrying used credit, **When** fewer than 60 days have elapsed since the credit
   was used, **Then** no additional charges are applied.
5. **Given** a card carrying used credit, **When** the 60-day interest-free period ends with credit
   still outstanding, **Then** daily interest at the fixed configured APR begins accruing on the
   outstanding credit (on each day's minimum credit balance) until it is repaid.

---

### Edge Cases

- What happens when an unverified (pending-KYC) user attempts a financial operation? It is blocked
  with a "verification required" message; no card or money movement is permitted.
- What happens when Diia is unavailable or KYC repeatedly fails during registration? The account
  stays unverified and blocked; the user may retry, and exhausted/ambiguous cases go to manual
  compliance review.
- What happens when a payment, deposit, or withdrawal is submitted twice (duplicate/retry)? The
  system MUST NOT process the same operation more than once (idempotency).
- What happens when the user lowers their personal credit limit below their current outstanding
  used credit? The change is accepted for future spending but does not force immediate repayment;
  no new spending is allowed above the new limit.
- What happens when a deposit and a withdrawal/payment are attempted concurrently? Balances MUST
  remain consistent and never go below zero available capacity.
- What happens to daily interest on a day the balance crosses between positive and negative? The
  day's minimum balance determines the basis: a day that dipped into credit earns no deposit
  interest on the negative portion, and (after grace) credit interest uses the day's minimum
  credit balance.
- What happens when the user's credit rating changes after the card exists? The company-offered
  maximum may be re-evaluated; an effective personal limit above a newly reduced maximum is
  brought down to the new maximum.
- What happens when a withdrawal exceeds the positive balance? The excess is taken as a cash
  advance against the credit line (up to the effective limit) with a 3% fee on the credit-funded
  portion; a withdrawal beyond the effective limit is declined.

## Requirements *(mandatory)*

### Functional Requirements

**Registration & KYC (AML)**

- **FR-031**: System MUST allow a new visitor to register for an account, creating it in a
  "pending verification" state.
- **FR-032**: System MUST require the user to complete identity verification (KYC) through Diia as
  part of registration before the account becomes active.
- **FR-033**: System MUST block all financial functionality (card creation, payments, deposits,
  withdrawals) for any account that has not passed KYC.
- **FR-034**: System MUST activate the account only after Diia KYC passes successfully.
- **FR-035**: System MUST, on KYC failure or rejection, keep the account unverified and blocked,
  notify the user, and allow the user to retry.
- **FR-036**: System MUST route repeated KYC failures or ambiguous results to a manual compliance
  review path rather than auto-rejecting permanently.
- **FR-037**: System MUST handle Diia unavailability gracefully by presenting a retry-later state,
  preserving the registration, and not activating the account.
- **FR-038**: System MUST store only the KYC verification result, a Diia verification reference,
  and minimal identifiers (full name, date of birth, national tax ID / RNOKPP reference); it MUST
  NOT store Diia document images.
- **FR-039**: KYC is performed once at registration; the system is NOT required to perform periodic
  re-verification or ongoing screening in this release.

**Card lifecycle**

- **FR-001**: System MUST allow an eligible, verified user to create a virtual credit card that
  starts in an active state.
- **FR-002**: System MUST allow the user to freeze an active card, after which new payments are
  declined.
- **FR-003**: System MUST allow the user to unfreeze a frozen card, after which payments are
  allowed again.

**Credit limits**

- **FR-004**: System MUST derive and present a company-suggested maximum credit limit based on the
  user's credit rating.
- **FR-005**: System MUST allow the user to set a personal credit limit that is greater than zero
  and less than or equal to the company-suggested maximum.
- **FR-006**: System MUST reject any personal limit that exceeds the company-suggested maximum and
  explain the allowed range.
- **FR-007**: System MUST treat the lower of (personal limit, company maximum) as the effective
  spending limit at all times.
- **FR-008**: System MUST re-evaluate and, if necessary, reduce the effective limit when the
  company-offered maximum decreases.

**Payments**

- **FR-009**: System MUST authorize a payment only when the card is active and the amount is
  within available capacity (positive balance plus remaining credit up to the effective limit).
- **FR-010**: System MUST decline payments that exceed available capacity or are made on a frozen
  card, without partially charging the user.
- **FR-011**: System MUST record every payment as a transaction and update the balance atomically.

**Deposits and withdrawals**

- **FR-012**: System MUST allow the user to deposit funds, increasing the positive balance.
- **FR-013**: System MUST allow the user to withdraw funds, drawing first on the available positive
  balance and then, for any excess, on the credit line (cash advance) up to the effective limit.
- **FR-014**: System MUST charge a 3% cash-advance fee on the credit-funded portion of a
  withdrawal, recorded as a fee transaction at the time of withdrawal. Withdrawals funded entirely
  by the positive balance incur no fee.
- **FR-015**: System MUST decline withdrawals that exceed the available capacity (positive balance
  plus remaining credit up to the effective limit) and notify the user.
- **FR-016**: System MUST record every deposit, withdrawal, and cash-advance fee as a transaction.

**Transactions and filtering**

- **FR-017**: System MUST record all money movements (payments, deposits, withdrawals, interest,
  and fees/charges) as transactions with type, amount, timestamp, and status.
- **FR-018**: System MUST allow the user to view their transactions and filter them by at least
  date range, transaction type, amount range, and status.
- **FR-019**: System MUST return an empty, clearly labeled result (not an error) when no
  transactions match the selected filters.

**Notifications**

- **FR-020**: System MUST notify the user of account changes (card created, frozen, unfrozen,
  limit changed, deposit posted, withdrawal posted, interest paid).
- **FR-021**: System MUST notify the user of failed operations (declined payment, rejected
  withdrawal, and similar).
- **FR-022**: Notifications MUST NOT expose sensitive data (e.g., full card number, credentials).

**Interest and credit period**

- **FR-023**: System MUST treat a positive card balance as a deposit and calculate interest on it
  daily at a fixed company-configured rate, using the minimum balance held during each day as the
  basis for that day's interest.
- **FR-024**: System MUST pay accrued deposit interest to the user and record it as an interest
  transaction.
- **FR-025**: System MUST provide a 60-day interest-free period on used credit during which no
  additional charges are applied.
- **FR-026**: System MUST, once the 60-day interest-free period elapses with credit still
  outstanding, accrue daily interest on the outstanding credit at a fixed company-configured APR
  (using the minimum credit balance held during each day) until the credit is repaid.

**Cross-cutting (per Constitution)**

- **FR-027**: System MUST authenticate the user and authorize every card operation so that a user
  can only act on their own card(s).
- **FR-028**: System MUST record an immutable audit entry for every security- and finance-relevant
  action (card lifecycle changes, limit changes, payments, deposits, withdrawals, interest,
  fees/charges), capturing who, what, when, and originating context.
- **FR-029**: System MUST treat all money operations as idempotent so that retries do not result
  in duplicate processing.
- **FR-030**: System MUST handle personal and financial data in accordance with GDPR, including
  data minimization, defined retention, and support for data-subject rights.

### Key Entities *(include if feature involves data)*

- **User / Account**: The registered cardholder and their account. Holds account state (pending
  verification / active / blocked), KYC verification status and reference, minimal identity
  attributes (full name, date of birth, national tax ID / RNOKPP reference), and a credit rating
  used to derive credit limits. Owns one or more cards.
- **KYC Verification**: The record of a Diia identity check — its result (pass/fail/pending),
  Diia verification reference, timestamp, and review status (e.g., awaiting manual compliance
  review). Gates account activation.
- **Virtual Card**: A non-physical card with a state (active/frozen), a company-offered maximum
  credit limit, a user-set personal limit, and a current balance.
- **Credit Limit**: The company-offered maximum (derived from credit rating) and the personal
  limit; the effective limit is the lower of the two.
- **Balance**: The current monetary position of a card — positive (deposit) or negative (used
  credit) — plus derived available spending capacity.
- **Transaction**: A recorded money movement of a given type (payment, deposit, withdrawal,
  interest, fee, charge), with amount, timestamp, status, and reference.
- **Fee**: A charge applied to the user — notably the 3% cash-advance fee on the credit-funded
  portion of a withdrawal — recorded as its own transaction.
- **Interest Accrual**: A daily computation of interest using the day's minimum balance — earned
  by the user on a positive (deposit) balance, or charged on outstanding credit after the 60-day
  interest-free period — recorded as an interest transaction.
- **Credit Period**: The 60-day interest-free window associated with outstanding used credit,
  after which daily credit interest begins.
- **Notification**: A message to the user about an account change or a failed operation.
- **Audit Record**: An immutable log of who performed what action, when, and in what context.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of financial operations (card creation, payment, deposit, withdrawal) attempted
  by an account that has not passed Diia KYC are blocked.
- **SC-002**: A verified user can create a card and have an effective credit limit configured in
  under 3 minutes from starting the request.
- **SC-003**: 100% of payments that exceed available capacity or are made on a frozen card are
  declined, with no partial charges.
- **SC-004**: Freeze and unfreeze take effect for new payment attempts within 5 seconds of the
  user's action.
- **SC-005**: 100% of money-movement operations (payments, deposits, withdrawals, interest) appear
  in the transaction history and the card balance reconciles exactly to the sum of transactions.
- **SC-006**: Filtered transaction queries return correct results for 100% of supported filter
  combinations.
- **SC-007**: Every account change and every failed operation generates a corresponding user
  notification within 1 minute.
- **SC-008**: Daily deposit interest is calculated and posted for 100% of days a card holds a
  positive balance, with the total paid reconciling to the day-by-day balances.
- **SC-009**: No additional charges are applied to outstanding used credit within the 60-day
  interest-free period in 100% of cases; daily credit interest is applied for 100% of days the
  credit remains outstanding beyond day 60.
- **SC-010**: A 3% cash-advance fee is applied to the credit-funded portion of 100% of withdrawals
  that draw on the credit line, and to 0% of withdrawals funded entirely by positive balance.
- **SC-011**: Every security- and finance-relevant action has a corresponding audit record (100%
  coverage), enabling full reconstruction of any transaction.
- **SC-012**: Duplicate submissions of the same operation result in exactly one processed
  transaction in 100% of retry cases.

## Assumptions

- Registration and KYC are in scope: identity verification is performed through Diia (Ukraine's
  government digital-identity app) at registration. Diia is an external dependency that returns a
  verification result; the system does not implement the identity check itself.
- Eligibility assumes users who can complete Diia KYC (i.e., Ukrainian residents holding a valid
  Diia identity, of legal age); broader eligibility is out of scope for this release.
- The credit rating is provided by the company's existing credit-assessment process; this feature
  consumes it to derive the maximum limit and does not compute the rating itself.
- The product operates in a single currency for the initial release; multi-currency is out of
  scope for v1.
- Each user may hold one virtual card in the initial release unless later expanded; the model does
  not assume a hard single-card constraint but scenarios are written for a primary card.
- Withdrawals draw first on the positive (deposit) balance, then on the credit line (cash advance)
  up to the effective limit; the credit-funded portion incurs a 3% fee charged once at withdrawal.
- The deposit interest rate, the post-grace credit APR, and the 3% cash-advance fee are
  company-configurable parameters; defaults are set by the business and not hard-coded into
  behavior. The deposit rate is a single fixed rate (not tiered or rating-dependent).
- Daily interest (deposit and post-grace credit) is computed on the minimum balance held during
  each closed calendar day, in the system's configured time zone.
- Notification delivery channel (in-app, email, push) is an implementation choice; the requirement
  is that the user is notified, not the specific channel.
- Settlement/clearing with external payment networks is abstracted; "payment" here means
  authorizing and recording a charge against the card.
