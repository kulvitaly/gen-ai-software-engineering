# API Reference

This document describes the public API surface for the Intelligent Customer Support System.

## Current Implementation Status

The project is currently through **Phase 8** of [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md). The API application starts successfully, exposes health checks, publishes OpenAPI/Scalar documentation, implements ticket CRUD endpoints, supports CSV/JSON/XML ticket imports, can auto-classify tickets, and includes final sample data, documentation, coverage report, and integration/performance coverage.

Base development URLs:

- HTTP: `http://localhost:5077`
- HTTPS: `https://localhost:7076`

Interactive API docs:

- Scalar API reference: `http://localhost:5077/scalar/v1`
- OpenAPI JSON: `http://localhost:5077/openapi/v1.json`

## Implemented Endpoints

### GET /health

Checks whether the Web API process is running.

**Response: `200 OK`**

```json
{
  "status": "ok",
  "service": "CustomerSupportSystem"
}
```

**cURL**

```bash
curl http://localhost:5077/health
```

### POST /tickets

Create a new support ticket.

Request validation uses DataAnnotations on the API request DTO. JSON uses `snake_case` names. Pass `auto_classify=true` as a query string to classify the ticket before it is saved; category and priority from the classifier replace the request values and classification metadata is stored.

**Request**

```json
{
  "customer_id": "customer-1",
  "customer_email": "ada@example.com",
  "customer_name": "Ada Lovelace",
  "subject": "Cannot access account",
  "description": "I cannot access my customer account after resetting my password.",
  "category": "account_access",
  "priority": "high",
  "status": "new",
  "tags": ["account", "login"],
  "metadata": {
    "source": "web_form",
    "browser": "Edge",
    "device_type": "desktop"
  },
  "assigned_to": null,
  "classification": null
}
```

**Responses**

- `201 Created` with the created ticket.
- `400 Bad Request` with validation details.

**cURL**

```bash
curl -X POST http://localhost:5077/tickets \
  -H "Content-Type: application/json" \
  -d "{\"customer_id\":\"customer-1\",\"customer_email\":\"ada@example.com\",\"customer_name\":\"Ada Lovelace\",\"subject\":\"Cannot access account\",\"description\":\"I cannot access my customer account after resetting my password.\",\"category\":\"account_access\",\"priority\":\"high\",\"status\":\"new\",\"tags\":[\"account\",\"login\"],\"metadata\":{\"source\":\"web_form\",\"browser\":\"Edge\",\"device_type\":\"desktop\"}}"
```

```bash
curl -X POST "http://localhost:5077/tickets?auto_classify=true" \
  -H "Content-Type: application/json" \
  -d "{\"customer_id\":\"customer-1\",\"customer_email\":\"ada@example.com\",\"customer_name\":\"Ada Lovelace\",\"subject\":\"Billing refund blocking launch\",\"description\":\"The payment refund is important and blocking our launch.\",\"category\":\"other\",\"priority\":\"medium\",\"status\":\"new\",\"tags\":[\"billing\"],\"metadata\":{\"source\":\"web_form\",\"browser\":\"Edge\",\"device_type\":\"desktop\"}}"
```

### POST /tickets/import

Bulk import tickets from CSV, JSON, or XML.

The endpoint accepts the raw file body and a required `format` query string: `csv`, `json`, or `xml`.

**Responses**

- `200 OK` with an import summary. Partial row failures are reported in the `failed` array; valid rows are still saved.
- `400 Bad Request` when `format` is unsupported or the file cannot be parsed as the selected format.

**Response**

```json
{
  "total": 3,
  "successful": 2,
  "failed": [
    {
      "record_number": 3,
      "errors": ["customer_email must be a valid email address"]
    }
  ]
}
```

**cURL**

```bash
curl -X POST "http://localhost:5077/tickets/import?format=csv" \
  -H "Content-Type: text/csv" \
  --data-binary @tests/fixtures/valid_tickets.csv
```

### GET /tickets

List tickets with optional filters:

- `category`
- `priority`
- `status`

**Response: `200 OK`**

```json
[
  {
    "id": "00000000-0000-0000-0000-000000000000",
    "customer_id": "customer-1",
    "customer_email": "ada@example.com",
    "customer_name": "Ada Lovelace",
    "subject": "Cannot access account",
    "description": "I cannot access my customer account after resetting my password.",
    "category": "account_access",
    "priority": "high",
    "status": "new",
    "created_at": "2026-05-16T12:00:00+00:00",
    "updated_at": "2026-05-16T12:00:00+00:00",
    "resolved_at": null,
    "assigned_to": null,
    "tags": ["account", "login"],
    "metadata": {
      "source": "web_form",
      "browser": "Edge",
      "device_type": "desktop"
    },
    "classification": null
  }
]
```

```bash
curl "http://localhost:5077/tickets?category=technical_issue&priority=high"
```

### GET /tickets/{id}

Retrieve a ticket by UUID.

**Responses**

- `200 OK` with the ticket.
- `404 Not Found` when the ticket does not exist.

### PUT /tickets/{id}

Partially update a ticket. All request fields are optional; provided values are validated with DataAnnotations and then applied by the application handler.

Manual category or priority updates clear stored auto-classification metadata so the response reflects the manual override.

**Request**

```json
{
  "subject": "Updated subject",
  "category": "feature_request",
  "priority": "low",
  "status": "resolved"
}
```

**Responses**

- `200 OK` with the updated ticket.
- `400 Bad Request` with validation details.
- `404 Not Found` when the ticket does not exist.

**cURL**

```bash
curl -X PUT http://localhost:5077/tickets/{id} \
  -H "Content-Type: application/json" \
  -d "{\"subject\":\"Updated subject\",\"category\":\"feature_request\",\"priority\":\"low\",\"status\":\"resolved\"}"
```


### DELETE /tickets/{id}

Delete a ticket.

**Responses**

- `200 OK` with `{ "id": "..." }`.
- `404 Not Found` when the ticket does not exist.

### POST /tickets/{id}/auto-classify

Run automatic category and priority classification for an existing ticket.

The classifier scans subject and description keywords, updates the ticket category and priority, stores classification metadata, and logs the decision.

**Responses**

- `200 OK` with the classification decision.
- `404 Not Found` when the ticket does not exist.

**Response**

```json
{
  "category": "technical_issue",
  "priority": "high",
  "confidence": 0.86,
  "reasoning": "Matched technical and high-priority keywords.",
  "keywords_found": ["error", "blocking"]
}
```

**cURL**

```bash
curl -X POST http://localhost:5077/tickets/{id}/auto-classify
```

## Ticket Model

The domain model is implemented under `src/Domain/Tickets`. The API maps application DTOs to snake_case HTTP response models.

The ticket model follows [TASKS.md](../TASKS.md):

```json
{
  "id": "UUID",
  "customer_id": "string",
  "customer_email": "email",
  "customer_name": "string",
  "subject": "string (1-200 chars)",
  "description": "string (10-2000 chars)",
  "category": "account_access | technical_issue | billing_question | feature_request | bug_report | other",
  "priority": "urgent | high | medium | low",
  "status": "new | in_progress | waiting_customer | resolved | closed",
  "created_at": "datetime",
  "updated_at": "datetime",
  "resolved_at": "datetime (nullable)",
  "assigned_to": "string (nullable)",
  "tags": ["array"],
  "metadata": {
    "source": "web_form | email | api | chat | phone",
    "browser": "string",
    "device_type": "desktop | mobile | tablet"
  },
  "classification": {
    "category": "account_access | technical_issue | billing_question | feature_request | bug_report | other",
    "priority": "urgent | high | medium | low",
    "confidence": "number 0-1",
    "reasoning": "string",
    "keywords_found": ["array"]
  }
}
```

## Error Response Format

Validation errors use ASP.NET Core validation problem responses:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Validation failed.",
  "errors": {
    "customer_email": ["Must be a valid email address."]
  }
}
```

## Notes for Future Updates

- Keep the OpenAPI document and this file aligned.
- Keep cURL examples runnable against `http://localhost:5077`.
- Add auto-classification endpoint examples when that phase is implemented.