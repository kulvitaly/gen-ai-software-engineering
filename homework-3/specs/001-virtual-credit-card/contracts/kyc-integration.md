# Integration Contract: Diia KYC Gateway

Diia is an external identity-verification provider. The domain depends only on the
`IKycGateway` **port** (Application layer); the Diia-specific adapter lives in Infrastructure.
This keeps the domain testable with a fake gateway and isolates provider changes.

> The exact Diia API (endpoints, auth, callback vs. polling) is confirmed with the provider during
> implementation. This contract models the behavior the application requires, per the clarified
> KYC rules (spec Session 2026-05-31).

## Port

```csharp
public interface IKycGateway
{
    // Returns Passed/Failed/Pending/ManualReview or a retriable Error on Diia unavailability.
    Task<Fin<KycOutcome>> VerifyAsync(KycRequest request, CancellationToken ct);
}

public sealed record KycRequest(Guid AccountId, string DiiaSessionToken);

public sealed record KycOutcome(
    KycResult Result,            // Passed | Failed | Pending | ManualReview
    string DiiaReference,        // provider reference to store (FR-038)
    MinimalIdentity? Identity);  // only when Passed; minimal fields only

public sealed record MinimalIdentity(string FullName, DateOnly DateOfBirth, string NationalTaxIdRef);
```

## Behavioral contract

| Situation | Gateway result | Application action |
|-----------|----------------|--------------------|
| Verification succeeds | `Passed` + reference + minimal identity | Persist result/reference/minimal IDs; activate account (FR-034, FR-038) |
| Verification rejected | `Failed` | Keep account blocked; notify; allow retry (FR-035) |
| Ambiguous / repeated failures | `ManualReview` | Flag for compliance review; account stays blocked (FR-036) |
| Diia unavailable / timeout | `Error` (retriable) | Return retry-later; preserve registration; do not activate (FR-037) |

## Resilience & security

- **Timeout + circuit breaker** around the Diia call; failures surface as a retriable `Error`,
  never an exception leaking provider detail (Principle I/VI).
- **Data minimization**: store only result + `DiiaReference` + minimal identity; **never** store
  document images or full payloads (FR-038, GDPR Principle II).
- **Audit**: every KYC attempt and outcome is written via the outbox to the audit store
  (Principle III), with sensitive values masked.
- **Idempotency**: a KYC submission for an account in `Pending`/`ManualReview` is safe to retry;
  outcomes are recorded with `AttemptCount`.
- **Third-party governance**: Diia is documented as a data processor with an appropriate
  data-processing safeguard (Security & Compliance Requirements).
