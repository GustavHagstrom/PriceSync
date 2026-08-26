# PriceSync

PriceSync is a product concept for a **neutral integration hub for B2B price synchronization between trading partners**.

The core idea is simple:

> A supplier publishes customer-specific prices once, and each customer can consume those prices in a standardized way from their own systems.

PriceSync is intended to sit between supplier and customer systems and replace many bespoke point-to-point price integrations with one common integration layer.

## Problem

B2B suppliers commonly distribute price updates through Excel, CSV, portals, email, EDI or bespoke integrations. Customers then manually import, map or re-enter this data into ERP, estimation, procurement and other internal systems.

This creates:

- manual administration
- stale prices
- duplicated integrations
- inconsistent formats
- mapping errors
- weak traceability of historical prices
- an N × M integration problem between suppliers and customers

Without a hub, every supplier/customer pair may need its own workflow or integration. With PriceSync, suppliers and customers integrate with the hub instead.

```text
Without PriceSync                 With PriceSync

Supplier A ── Customer 1          Suppliers ──► PriceSync ──► Customers
Supplier A ── Customer 2
Supplier B ── Customer 1
Supplier B ── Customer 2
...
```

## Product vision

```text
Supplier ERP / Excel / API / SFTP
                |
                v
        +----------------+
        |   PriceSync    |
        |----------------|
        | Ingest         |
        | Map            |
        | Normalize      |
        | Validate       |
        | Version        |
        | Authorize      |
        | Transform      |
        | Distribute     |
        +----------------+
                |
                v
 Customer ERP / Estimation / Procurement
```

The long-term goal is that systems can ask a simple question:

> What is Supplier X's agreed price for Customer Y for article Z, and when is that price valid?

## Product principles

1. **Integration hub for B2B prices**  
   PriceSync connects supplier and customer systems and reduces the need for bespoke point-to-point integrations.

2. **Price synchronization only**  
   Avoid becoming a PIM, ERP, webshop, document portal or product catalog.

3. **API-first**  
   The canonical platform should be technical infrastructure that other systems integrate with.

4. **Extremely low supplier onboarding friction**  
   Suppliers should not need a complete product model. Existing Excel/CSV exports should be enough to start.

5. **Coexists with existing tools**  
   Tradeplace, GS1, ETIM, PIM systems and ERP systems can remain in place.

6. **Minimal canonical model**  
   Store only the commercial data required to synchronize prices.

7. **Versioning is first-class**  
   It should always be possible to determine which price was published and valid at a specific point in time.

8. **Do not require integration on day one**  
   Supplier maturity may progress from Excel/CSV → SFTP → API → native ERP integration.

9. **Adapters isolate external formats**  
   Supplier and customer formats should be translated at the platform boundary rather than leaking into the canonical price model.

## Candidate minimal model

```json
{
  "articleId": "ABC-123",
  "price": 42.73,
  "currency": "SEK",
  "unit": "ST",
  "validFrom": "2026-09-01"
}
```

Relationship-level metadata can identify supplier, customer, agreement and price-set version.

## Current stage

**Idea / market validation.**

No implementation decisions should be treated as fixed yet. The highest-risk questions are market and adoption related, not technical.

See [`docs/product-vision.md`](docs/product-vision.md), [`docs/business-model.md`](docs/business-model.md), [`docs/architecture.md`](docs/architecture.md) and [`research/hypotheses.md`](research/hypotheses.md).
