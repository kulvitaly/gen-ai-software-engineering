# Implementation Plan: Bug 002

**Test command**: dotnet test

This plan fixes the root cause confirmed in `research/verified-research.md`
(Research Quality: EXCELLENT, Overall: PASS): ticket classification keyword
matching is case-sensitive. Keyword rules are all lowercase
(`TicketClassifier.cs:12-19`, `:21-26`) and the classification text is built from
raw, non-normalized `Subject`/`Description` (`:32`), while `ContainsKeyword`
(`:67-70`) calls `text.Contains(keyword)`, which uses the default ordinal,
case-sensitive comparison. As a result, tickets whose text uses different casing
(e.g. "Login", "PASSWORD", "Critical") fail to match and are misclassified as
`Other`/`Medium`.

The verified research also confirms there is **no** "stored twice" defect
(`TicketHandlers.cs:48` calls `repository.Add` exactly once); therefore no change
is made for that symptom.

The fix is a single, minimal change: make the keyword `Contains` check
case-insensitive using `StringComparison.OrdinalIgnoreCase`. This keeps the
lowercase rule tables unchanged and does not alter the raw text construction.

## Change 1 — Make keyword matching case-insensitive

**File**: `src/Application/Tickets/TicketClassifier.cs`

**Location**: `src/Application/Tickets/TicketClassifier.cs:67-70`

**Before**
```csharp
    private static bool ContainsKeyword(string text, string keyword)
    {
        return text.Contains(keyword);
    }
```

**After**
```csharp
    private static bool ContainsKeyword(string text, string keyword)
    {
        return text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
```

**Reason**: `string.Contains(string)` uses an ordinal, case-sensitive comparison,
so lowercase keyword rules (`TicketClassifier.cs:12-19`, `:21-26`) only match text
that is already lowercase. Passing `StringComparison.OrdinalIgnoreCase` makes the
match case-insensitive, so tickets are classified correctly regardless of the
casing in `Subject`/`Description`. This is the minimal change targeting the
single offending line identified as the root cause; the rule tables and the text
construction at line 32 remain unchanged.

## Verification

1. Apply Change 1 and run the test command: `dotnet test`. All tests should pass,
   including any tests exercising classification with mixed/upper case input.
2. Confirm correct classification for casing variations by exercising
   `TicketClassifier.Classify` (directly via a test or via the Create ticket
   endpoint) with inputs such as:
   - Subject/Description containing `"Login"` or `"PASSWORD"` → expect
     `Category = AccountAccess` (not `Other`).
   - Text containing `"Critical"` or `"Security"` → expect
     `Priority = Urgent` (not `Medium`).
   - Text containing `"Suggestion"` → expect `Category = FeatureRequest` and
     `Priority = Low`.
3. Confirm that already-lowercase inputs still classify exactly as before (no
   regression), e.g. `"login"` → `AccountAccess`.
4. Confirm the matched `Keywords`/`Reasoning` in the returned
   `TicketClassification` reflect the keywords that were found, demonstrating the
   case-insensitive match took effect.
