# Architecture Principles

## Architectural intent

PriceSync should be API-first **integration-hub infrastructure** for B2B price synchronization, with a minimal canonical model and multiple low-friction ingestion and delivery options.

The architectural goal is to reduce N × M point-to-point integrations by placing a stable canonical model and adapter boundary between supplier and customer systems.

```text
Supplier systems
ERP / Excel / CSV / API / SFTP
          |
          v
   Ingestion adapters
          |
          v
+-------------------------+
|        PriceSync        |
|-------------------------|
| Identity                |
| Trading relationships   |
| Canonical price model   |
| Validation              |
| Versioning              |
| Authorization           |
| Change tracking         |
| Audit                    |
+-------------------------+
          |
          v
 Distribution adapters
          |
          v
Customer systems
ERP / Estimation / BI / API / Excel
```

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
- Transformation at system boundaries

## Architectural boundaries

The canonical PriceSync model should not depend on supplier- or customer-specific file layouts, ERP schemas or transport protocols.

External formats belong in adapters:

```text
External format -> Adapter -> Canonical model -> Adapter -> External format
```

This allows an organization to change from Excel to SFTP or API without changing the semantics of its PriceSync relationship.

## Candidate domain model

```text
Organization
  └── TradingRelationship
        └── PriceAgreement (optional)
              └── PriceSet
                    └── Price[]
```

Organizations may potentially participate as suppliers, customers, or both. The exact aggregate boundaries are intentionally undecided.

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

### Hypothesis: API draft batching

Canonical JSON publication may assemble one price draft through multiple idempotent API batches before final validation and atomic publication. A reasonable initial limit to test is 5,000 entries or 5 MiB of JSON per request, whichever is reached first.

This is a technical starting hypothesis rather than a domain constraint. Final limits should remain configurable and be established using representative payloads and load tests. Manual Excel/CSV upload uses a separate file-processing flow and is not subject to the JSON batch limit.

## Integration adapters

The canonical platform may be API-first while still accepting simpler transports:

```text
Excel/CSV ─┐
SFTP ──────┼──> ingestion adapters ──> canonical PriceSync model
API ───────┘
```

On the customer side:

```text
canonical PriceSync model ──> distribution adapters ──┬──> REST API
                                                       ├──> Webhook
                                                       ├──> CSV / Excel
                                                       └──> ERP-specific format
```

This distinction is important: API-first must not become API-only if API-only creates onboarding friction.

### Import profiles

File-based ingestion should support reusable mapping profiles, conceptually:

```text
ImportProfile
├── OrganizationId
├── FileType
├── Sheet / header configuration
├── Field mappings
└── Default values
```

Import profiles are adapters/configuration around the canonical model, not part of the price domain itself.

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

The technical hub is considered feasible with standard integration architecture.

The dominant risks are expected to be:

- semantic edge cases in real-world pricing
- supplier onboarding
- authentication / authorization between companies
- mapping legacy source formats
- trust and audit requirements
- network adoption
- operational complexity from supporting many external formats/connectors
