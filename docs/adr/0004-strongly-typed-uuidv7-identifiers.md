# Strongly typed UUIDv7 domain identifiers

PriceSync domain identifiers use concept-specific Guid wrappers and are stored as native PostgreSQL `uuid` values. New identities owned by PriceSync are generated in the application as UUIDv7, providing decentralized creation and better index locality than random UUIDv4 values; IDs remain identifiers rather than secrets, and contexts exchange only the exact strongly typed IDs their contracts require.
