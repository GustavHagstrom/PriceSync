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
| Trading Relationships | Create, End, and Get implemented on the work branch in [PR #50](https://github.com/GustavHagstrom/PriceSync/pull/50); supplier list implementation in [PR #53](https://github.com/GustavHagstrom/PriceSync/pull/53) |
| Price ingestion, agreements, publication, and consumption | Domain documentation and module scaffolding; workflows remain to be implemented |
| Production authentication | Pending; the default host does not authenticate users, so protected endpoints return `401` |

## Technology and architecture

C# and .NET 10, ASP.NET Core Minimal APIs, PostgreSQL, EF Core/Npgsql, xUnit, and Testcontainers.

PriceSync starts as a modular monolith. Modules own their domain logic, HTTP endpoints, and persistence. Cross-module collaboration uses explicit contracts rather than shared entities or direct access to another module's tables.

This keeps domain ownership explicit within one deployable application. The reasoning is recorded in the [architecture decisions](docs/adr/), with more detail in the [architecture overview](docs/architecture.md).

## Code highlights

### Enforced module boundaries — on main

[Architecture tests](tests/PriceSync.ArchitectureTests/ArchitectureBoundaryTests.cs) check project dependency rules, the public Organization & Access contracts, and ownership of its persistence schema. Modules can depend on other modules' contracts, while implementation projects remain isolated.

### Organization-scoped access — on main

The [Get Organization handler](src/PriceSync.Modules.OrganizationAccess/Features/GetOrganization/GetOrganizationHandler.cs) scopes its query to the current person's active membership. The corresponding [API tests](tests/PriceSync.Modules.OrganizationAccess.IntegrationTests/GetOrganizationApiTests.cs) exercise access behavior using PostgreSQL and a test authentication handler.

### Database guarantees under concurrent requests — in PR #50

Trading Relationships uses a partial unique index to allow only one active relationship per supplier/customer pair. Create handles a competing insert by returning the existing relationship. Ending a relationship preserves its history and original end timestamp on repeated requests.

See the [Create handler](https://github.com/GustavHagstrom/PriceSync/blob/issue-12-trading-relationships/src/PriceSync.Modules.TradingRelationships/Features/Create/CreateHandler.cs), [database constraints](https://github.com/GustavHagstrom/PriceSync/blob/issue-12-trading-relationships/src/PriceSync.Modules.TradingRelationships/Persistence/Configurations/TradingRelationshipConfiguration.cs), and integration tests for [Create](https://github.com/GustavHagstrom/PriceSync/blob/issue-12-trading-relationships/tests/PriceSync.Modules.TradingRelationships.IntegrationTests/CreateApiTests.cs) and [End](https://github.com/GustavHagstrom/PriceSync/blob/issue-12-trading-relationships/tests/PriceSync.Modules.TradingRelationships.IntegrationTests/EndApiTests.cs).

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
```

## Run the API host

```sh
dotnet run --project src/PriceSync.Api
```

Use the URL printed at startup. `GET /` returns the service name. The protected organization endpoints require authentication that is currently supplied only by the integration-test host; there is no interactive authenticated demo yet.

For database-backed development, configure `ConnectionStrings:OrganizationAccess` for your PostgreSQL instance. The default configuration points to a local `pricesync` database; the API host does not automatically apply migrations or seed data.

## Further reading

- [Documentation index](docs/readme.md)
- [Domain model and bounded contexts](docs/Domain/readme.md)
- [Domain glossary](docs/Context.md)
- [Product vision and onboarding approach](docs/product-vision.md)
- [Validation hypotheses](research/hypotheses.md)
