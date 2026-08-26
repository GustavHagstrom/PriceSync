# Architecture Principles

## Architectural intent

PriceSync should be API-first infrastructure with a minimal canonical model and multiple low-friction ingestion options.

## Core responsibilities

- Supplier/customer identity
- Trading relationship authorization
- Price-set ingestion
- Validation
- Canonical normalization
- Versioning
- Distribution
- Change tracking
- Auditability

## Candidate domain model

```text
Supplier
  └── TradingRelationship
        └── PriceAgreement (optional)
              └── PriceSet
                    └── Price[]
```

The exact aggregate boundaries are intentionally undecided.

## Candidate price fields

Minimum likely fields:

- Supplier article identifier
- Price
- Currency
- Price unit
- Valid from

Potential optional fields:

- Valid to
- GTIN
- Customer article identifier
- Minimum quantity
- Agreement identifier
- Quantity / packaging basis

Avoid adding descriptive product data unless it is strictly required for synchronization.

## API direction

Illustrative only:

```http
POST /v1/price-sets
GET  /v1/suppliers/{supplierId}/prices/current
GET  /v1/suppliers/{supplierId}/prices/changes?since=...
GET  /v1/suppliers/{supplierId}/prices/{articleId}
```

Possible events:

```text
price-set.published
price.changed
price.removed
```

## Ingestion

The canonical platform may be API-first while still accepting simpler transports:

```text
Excel/CSV ─┐
SFTP ──────┼──> ingestion adapters ──> canonical PriceSync model
API ───────┘
```

This distinction is important: API-first must not become API-only if API-only creates onboarding friction.

## Non-goals

Do not initially implement:

- product images
- product descriptions
- document management
- ETIM-style classification
- inventory
- ordering
- invoicing
- quoting
- general procurement workflows

## Technical risk assessment

The technical platform is considered feasible with standard integration architecture.

The dominant risks are expected to be:

- semantic edge cases in real-world pricing
- supplier onboarding
- authentication / authorization between companies
- mapping legacy source formats
- trust and audit requirements
- network adoption
