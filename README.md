# PriceSync

PriceSync is a product concept for **B2B price synchronization between trading partners**.

The core idea is simple:

> A supplier publishes customer-specific prices once, and each customer can consume those prices in a standardized way from their own systems.

PriceSync is intended to be infrastructure rather than a PIM, ERP, webshop or product portal.

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

## Product vision

```text
Supplier ERP / Excel / API / SFTP
                |
                v
        +----------------+
        |   PriceSync    |
        |----------------|
        | Normalize      |
        | Validate       |
        | Version        |
        | Authorize      |
        | Distribute     |
        +----------------+
                |
                v
 Customer ERP / Estimation / Procurement
```

The long-term goal is that systems can ask a simple question:

> What is Supplier X's agreed price for Customer Y for article Z, and when is that price valid?

## Product principles

1. **Price synchronization only**  
   Avoid becoming a PIM, ERP, webshop, document portal or product catalog.

2. **API-first**  
   The canonical platform should be technical infrastructure that other systems integrate with.

3. **Extremely low supplier onboarding friction**  
   Suppliers should not need a complete product model. Existing Excel/CSV exports should be enough to start.

4. **Coexists with existing tools**  
   Tradeplace, GS1, ETIM, PIM systems and ERP systems can remain in place.

5. **Minimal canonical model**  
   Store only the commercial data required to synchronize prices.

6. **Versioning is first-class**  
   It should always be possible to determine which price was published and valid at a specific point in time.

7. **Do not require integration on day one**  
   Supplier maturity may progress from Excel/CSV → SFTP → API → native ERP integration.

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
