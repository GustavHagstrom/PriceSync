# PriceSync

A .NET backend project for standardizing and synchronizing customer-specific B2B prices between suppliers and their customers. Under active development.

## Why I am building this

The idea grew out of my work at a house manufacturer, where the organization manages around 100 or more B2B price lists. I spend significant time transferring supplier prices into our internal digital systems. Each supplier uses a different format, making the process largely manual.

PriceSync aims to turn those varying formats into standardized price data that customer systems can import consistently, with automation as a next step.

The broader vision is a neutral integration hub: suppliers publish prices once, and customers consume them through a common interface rather than maintaining separate integrations for every supplier.

```text
Supplier Excel / CSV / API
           |
           v
       PriceSync
  Normalize and validate
  Version and authorize
           |
           v
Customer ERP / estimation / purchasing systems
```

Suppliers should be able to start with their existing files. Format-specific mappings belong at the system boundary, keeping the core price model independent of individual spreadsheets and ERP systems.

The manual import problem comes from my own experience. The proposed solution, its time savings, and its value to other organizations remain hypotheses to validate. See the [product vision](docs/product-vision.md) and [validation hypotheses](research/hypotheses.md).

## Current implementation

The repository currently contains the backend foundation. Price import, publication, and synchronization are planned functionality.

| Area | Status |
| --- | --- |
| Modular monolith | Module projects, separate contracts, and architecture tests on `main` |
| Organization & Access | Domain rules, PostgreSQL persistence and migrations, and two read endpoints on `main` |
| Trading Relationships | Create, End, Get, and Supplier/Customer lists with PostgreSQL persistence and concurrency tests on `main` |
| Logical Price Lists | Supplier-owned General and Customer-Specific Net lists: authenticated create/read, immutable scope, PostgreSQL persistence and complete-set verification for Agreements |
| Price ingestion, agreements, publication, and consumption | Domain documentation and module scaffolding; workflows remain to be implemented |
| Production authentication | Pending; the default host does not authenticate users, so protected endpoints return `401` |

## Technology and architecture

C# and .NET 10, ASP.NET Core Minimal APIs, PostgreSQL, EF Core/Npgsql, xUnit, and Testcontainers.

PriceSync starts as a modular monolith. Modules own their domain logic, HTTP endpoints, and persistence. Cross-module collaboration uses explicit contracts rather than shared entities or direct access to another module's tables.

This keeps domain ownership explicit within one deployable application. The reasoning is recorded in the [architecture decisions](docs/adr/), with more detail in the [architecture overview](docs/architecture.md).

## Code highlights

### Enforced module boundaries — on main

[Architecture tests](tests/PriceSync.ArchitectureTests/ModuleDependencyTests.cs) check project dependency rules, the public Organization & Access contracts, and ownership of its persistence schema. Modules can depend on other modules' contracts, while implementation projects remain isolated.

### Organization-scoped access — on main

The [Get Organization handler](src/PriceSync.Modules.OrganizationAccess/Features/GetOrganization/GetOrganizationHandler.cs) scopes its query to the current person's active membership. The corresponding [API tests](tests/PriceSync.Modules.OrganizationAccess.IntegrationTests/GetOrganizationApiTests.cs) exercise access behavior using PostgreSQL and a test authentication handler.

### Database guarantees under concurrent requests — on main

Trading Relationships uses a partial unique index to allow only one active relationship per supplier/customer pair. Create handles a competing insert by returning the existing relationship. Ending a relationship preserves its history and original end timestamp on repeated requests.

See the [Create handler](src/PriceSync.Modules.TradingRelationships/Features/Create/CreateHandler.cs), [database constraints](src/PriceSync.Modules.TradingRelationships/Persistence/Configurations/TradingRelationshipConfiguration.cs), and integration tests for [Create](tests/PriceSync.Modules.TradingRelationships.IntegrationTests/CreateApiTests.cs) and [End](tests/PriceSync.Modules.TradingRelationships.IntegrationTests/EndApiTests.cs).

## Build and test

From the repository root, with the .NET 10 SDK installed:

```sh
dotnet build PriceSync.slnx
dotnet test tests/PriceSync.ArchitectureTests
dotnet test tests/PriceSync.Modules.OrganizationAccess.UnitTests
```

Integration tests require a running Docker-compatible container runtime. Testcontainers starts PostgreSQL 16 and applies migrations; a separately configured database is not required for these tests.

```sh
dotnet test tests/PriceSync.Modules.OrganizationAccess.IntegrationTests
dotnet test tests/PriceSync.Modules.PricePublication.IntegrationTests
```

## Run the API host

```sh
dotnet run --project src/PriceSync.Api
```

Use the URL printed at startup. `GET /` returns the service name. The protected organization endpoints require authentication that is currently supplied only by the integration-test host; there is no interactive authenticated demo yet.

For database-backed development, configure `ConnectionStrings:OrganizationAccess`, `ConnectionStrings:TradingRelationships` and `ConnectionStrings:PricePublication` for your PostgreSQL instance. The default configuration points to a local `pricesync` database; the API host does not automatically apply migrations or seed data.

### Logical Price Lists

`POST /api/v1/price-lists` creates a Supplier-owned logical list before any prices or versions exist. Supply the acting Supplier in the `organization_id` header. OrganizationAccess requires an active Owner to create and an active Member (including Owner) to read through `GET /api/v1/price-lists/{id}`. Reads are private to the owning Supplier.

```json
{
  "name": "General SEK",
  "type": "general",
  "currency": "SEK",
  "market": "Sweden",
  "customerOrganizationId": null
}
```

The supported types are `general` (no Customer binding) and `customerSpecificNet` (exactly one active Customer Organization). Name is display metadata, limited to 200 characters; Market is required and limited to 100 characters. Currency uses three uppercase letters. Each creation assigns a new UUIDv7, even for identical names and scope. Owner, type, currency, market and applicable Customer cannot change; both EF mappings and a PostgreSQL trigger protect that scope. There is no scope-update endpoint.

Price Publication exposes `IPriceListScopeVerifier` for trusted module callers. Pass the intended Supplier/Customer and the complete collection of `PriceListReference` values, each with an expected `PriceListType`. Only `Verified` returns immutable scopes for the complete request. `Invalid` returns structured invalid references; `Unavailable` is explicitly retryable. No partial result, Price List Version, publication, or scope lock is involved. The verifier does not authorize HTTP actors; its caller must enforce the caller's own workflow authority before using it.

Price Publication owns the `price_publication` PostgreSQL schema and its own migration history. To apply its migrations with the EF CLI, configure the design-time connection separately:

```powershell
$env:PRICESYNC_PRICE_PUBLICATION_DESIGN_TIME_CONNECTION_STRING = "Host=localhost;Database=pricesync;Username=pricesync"
dotnet ef database update --project src/PriceSync.Modules.PricePublication
```

## How to read the documentation

For a quick technical review, start with the code highlights above, then read the [architecture decisions](docs/adr/) alongside the linked code and tests.

For a fuller understanding, follow this order:

1. [Product vision](docs/product-vision.md) — the intended workflow, product boundaries, and approach to onboarding.
2. [Domain map](docs/Domain/readme.md) — ownership and relationships between business areas. Follow the Organization & Access and Trading Relationships pages for the implemented domain areas.
3. [Architecture](docs/architecture.md) and [decision records](docs/adr/) — module boundaries, persistence ownership, and the reasoning behind technical choices.
4. [Validation hypotheses](research/hypotheses.md) and [business model](docs/business-model.md) — assumptions about adoption and value that still need evidence.

Use the [domain glossary](docs/Context.md) as a reference when a business term is unfamiliar; it does not need to be read from start to finish. The [documentation index](docs/readme.md) explains the documentation structure and conventions.

Domain documentation describes the intended business rules, including areas not yet implemented. Architecture documentation includes both accepted decisions and explicitly labelled proposals. Use the current implementation table above to distinguish design from working code. Retained business rules and technical decisions live in the domain documentation and architecture overview; the Organization-creation application slice is explicitly marked as planned.
