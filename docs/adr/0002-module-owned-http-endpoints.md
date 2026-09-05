# Module-owned HTTP endpoints

PriceSync modules define and map the Minimal API endpoints for use cases they own, exposing only a narrow `Map{Module}Endpoints(...)` composition surface alongside service registration. `PriceSync.Api` remains the executable host and owns host-level HTTP concerns, authentication integration, middleware, and route composition; this keeps each vertical slice inside its bounded-context module without making handlers or API DTOs public cross-module contracts.
