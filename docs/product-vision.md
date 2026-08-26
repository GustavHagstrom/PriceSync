# Product Vision

## Vision

PriceSync should become neutral infrastructure for synchronizing agreed B2B prices between trading partners.

A supplier should be able to publish prices once. Customers should be able to consume those prices through their own ERP, estimation, purchasing or analytics systems without separate point-to-point integrations.

## What PriceSync is

- A technical price-distribution network
- A canonical model for customer-specific B2B prices
- A synchronization and versioning layer
- An authorization boundary between supplier and customer
- An integration point for software vendors and internal systems

## What PriceSync is not

- PIM
- ERP
- webshop
- procurement suite
- product catalog
- image/document repository
- product classification system
- ordering platform

Product data can live in systems such as GS1, ETIM, Tradeplace, PIM or the supplier's ERP. PriceSync should only require enough identity to associate a commercial price with an article.

## Desired end state

A customer system should be able to show:

```text
Suppliers
-----------------------------
Supplier A      synchronized
Supplier B      synchronized
Supplier C      synchronized
Supplier D      not connected
```

The user may never need to visit PriceSync directly.

## Adoption principle

Connecting should be easier than continuing to exchange price lists manually.

Possible supplier maturity path:

```text
Existing Excel/CSV
       ↓
Scheduled file delivery / SFTP
       ↓
REST API
       ↓
Native ERP connector
```

All paths should result in the same canonical PriceSync representation.

## Initial vertical hypothesis

Construction-related suppliers and buyers, including house manufacturers, are a candidate initial market because price-list handling appears highly fragmented and customer-specific pricing is common.

This is a hypothesis to validate, not a final market restriction.
