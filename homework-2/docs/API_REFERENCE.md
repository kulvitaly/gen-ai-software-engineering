# API Reference

This document describes the public API surface for the Intelligent Customer Support System.

## Current Implementation Status

The project is currently at **Phase 0** of [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md). The API application starts successfully, exposes health checks, and publishes OpenAPI/Scalar documentation. Ticket management endpoints are planned but not implemented yet.

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

## Planned Ticket Endpoints

The endpoints below are required by [TASKS.md](../TASKS.md) and will be implemented in later phases. Until then, they should be treated as a contract target, not as available behavior.

### POST /tickets

Create a new support ticket.

Expected success status: `201 Created`

### POST /tickets/import

Bulk import tickets from CSV, JSON, or XML.

Expected response includes:

```json
{
  "total": 3,
  "successful": 2,
  "failed": [
    {
      "record": 3,
      "errors": ["customer_email must be a valid email address"]
    }
  ]
}
```

### GET /tickets

List tickets with filtering. Planned filters include `category`, `priority`, and `status`.

Example planned request:

```bash
curl "http://localhost:5077/tickets?category=technical_issue&priority=high"
```

### GET /tickets/{id}

Retrieve a ticket by UUID.

Expected statuses: `200 OK`, `404 Not Found`

### PUT /tickets/{id}

Update a ticket.

Expected statuses: `200 OK`, `400 Bad Request`, `404 Not Found`

### DELETE /tickets/{id}

Delete a ticket.

Expected statuses: `200 OK` or `204 No Content`, `404 Not Found`

### POST /tickets/{id}/auto-classify

Run automatic category and priority classification for an existing ticket.

Expected response includes:

```json
{
  "category": "technical_issue",
  "priority": "high",
  "confidence": 0.86,
  "reasoning": "Matched technical and high-priority keywords.",
  "keywords_found": ["error", "blocking"]
}
```

## Ticket Model

The planned ticket model follows [TASKS.md](../TASKS.md):

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
  }
}
```

## Error Response Format

The final API should use a consistent JSON error response. Prefer ASP.NET Core `ProblemDetails` for validation and not-found errors:

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

- Replace this planned-contract content with exact request/response examples as each phase is implemented.
- Keep the OpenAPI document and this file aligned.
- Keep cURL examples runnable against `http://localhost:5077`.