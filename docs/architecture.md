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

## Modular monolith implementation

PriceSync starts as a pragmatic modular monolith. The established bounded contexts are implemented as module boundaries inside one deployable system, not as independently deployed services.

Initial solution structure:

```text
src/
  PriceSync.Api/
  PriceSync.Application/
  PriceSync.SharedKernel/
  PriceSync.Modules.OrganizationAccess/
  PriceSync.Modules.OrganizationAccess.Contracts/
  PriceSync.Modules.TradingRelationships/
  PriceSync.Modules.TradingRelationships.Contracts/
  PriceSync.Modules.Agreements/
  PriceSync.Modules.Agreements.Contracts/
  PriceSync.Modules.PricePublication/
  PriceSync.Modules.PricePublication.Contracts/
  PriceSync.Modules.CustomerPricing/
  PriceSync.Modules.CustomerPricing.Contracts/
  PriceSync.Modules.RetrievalConsumption/
  PriceSync.Modules.RetrievalConsumption.Contracts/
  PriceSync.Modules.IntegrationConfiguration/
  PriceSync.Modules.IntegrationConfiguration.Contracts/

tests/
  PriceSync.ArchitectureTests/
```

### Host and module responsibilities

`PriceSync.Api` is the executable host and composition root. It owns host-level HTTP concerns, authentication integration, configuration, process startup, and route composition.

Each module owns its Minimal API endpoint definitions together with its internal use cases, domain model, persistence mappings, validation, and module-specific service registration. A module exposes narrow composition surfaces such as `AddAgreementsModule(...)` and `MapAgreementsEndpoints(...)`; the host calls them but does not compose the module's internals by hand.

`PriceSync.Application` is a thin orchestration layer for workflows that genuinely cross module boundaries. It depends on module contracts and shared technical building blocks, not on module implementation projects. Use cases that belong wholly inside one bounded context stay inside that module.

Public HTTP endpoints use an `/api/v1` route prefix from the first exposed slice. Modules map endpoints beneath that host convention through route groups; a dedicated API-versioning library is introduced only when multiple live contract versions require it.

### Module implementation conventions

Each module implementation project uses its root namespace, for example `PriceSync.Modules.Agreements`, only for the public composition surfaces that the API host needs to call. Initially these should be limited to service registration and endpoint mapping extensions, such as `AddAgreementsModule(...)` and `MapAgreementsEndpoints(...)`.

Implementation types inside a module should be `internal` by default and placed under purpose-specific subnamespaces as they are introduced, for example:

- `PriceSync.Modules.Agreements.Application`
- `PriceSync.Modules.Agreements.Domain`
- `PriceSync.Modules.Agreements.Infrastructure`

Do not create parallel shared `Application`, `Domain`, or `Infrastructure` projects for all modules. A subnamespace should appear when a module has real implementation code that belongs there, not as empty structure.

Types that must be consumed outside the module belong in the sibling `.Contracts` project. Promoting a type from a module implementation project to a contract is a boundary decision and should keep the type application-facing rather than turning it into shared domain state.

### Contracts and dependency rules

Every module has a sibling `.Contracts` project. Contracts contain application-facing integration surfaces such as IDs, request/response DTOs, ports, query interfaces, or notifications needed by other modules or the application layer.

Contracts must not contain aggregates, EF entities, persistence models, internal domain services, or a shared PriceSync domain model. Cross-context collaboration uses exact IDs and version references rather than direct object sharing.

Allowed dependencies:

- Module implementation projects may reference their own contracts, other module contracts, and `PriceSync.SharedKernel`.
- Module implementation projects must not reference other module implementation projects.
- Module contracts must not reference module implementation projects.
- `PriceSync.SharedKernel` must not reference modules.
- `PriceSync.Application` may reference module contracts and `PriceSync.SharedKernel`.
- `PriceSync.Api` may reference `PriceSync.Application` and module implementation projects for composition.

### Persistence ownership

Each module owns its own persistence scope. The initial implementation uses PostgreSQL through EF Core and Npgsql. Sharing one physical PostgreSQL database does not weaken module ownership or turn persistence into a shared domain boundary.

Within the shared database, each module owns a separate PostgreSQL schema, including its tables, indexes, and EF migrations history. Modules do not create cross-schema EF navigations or foreign keys and do not query another module's schema directly.

Modules must not share persistence entities, EF navigation properties, or direct table/model access across module boundaries. Cross-module reads and orchestration go through contracts using explicit identifiers and immutable version references.

### Shared kernel

`PriceSync.SharedKernel` stays intentionally small and technical. It may contain generic building blocks such as result/error types, clock abstractions, and similar infrastructure-neutral primitives.

It must not contain PriceSync business concepts such as Organization, Supplier, Customer, Price List, Agreement, Version Validity, or shared persistence entities. Domain concepts start in the module that owns their behavior and are lifted only if repeated use proves they are truly shared with the same semantics.

### Boundary verification

Architecture tests should enforce the dependency rules above. The initial skeleton for each module should compile, expose a registration extension, and avoid implementing aggregates or use cases that belong to later implementation issues.

Architecture tests use xUnit as the repository's default .NET test framework. FluentAssertions is the default assertion library for tests where expressive failure messages make architectural or domain rules easier to read. Project-reference rules are tested directly from `.csproj` files; introduce a type-level architecture testing library such as NetArchTest or ArchUnitNET only when the rule being verified depends on assemblies, namespaces, or type visibility.

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
POST /api/v1/price-sets
GET  /api/v1/suppliers/{supplierId}/prices/current
GET  /api/v1/suppliers/{supplierId}/prices/changes?since=...
GET  /api/v1/suppliers/{supplierId}/prices/{articleId}
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
