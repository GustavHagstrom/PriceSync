# PostgreSQL for module persistence

PriceSync uses PostgreSQL through EF Core and Npgsql for its initial durable persistence. Each module owns its DbContext, mappings, and migrations even when modules share one physical database; this supports transactional and uniqueness guarantees with a production-real persistence model and avoids temporary in-memory repositories that would not exercise those guarantees.
