# Product Vision

## Vision

PriceSync should become a **neutral integration hub for synchronizing agreed B2B prices between trading partners**.

A supplier should be able to publish prices once. Customers should be able to consume those prices through their own ERP, estimation, purchasing or analytics systems without separate point-to-point integrations.

The product is not primarily a price-list portal. The hub itself is the product: a common technical integration layer between supplier and customer systems.

## Why a hub

The current model often creates an N × M integration problem:

```text
Supplier A ── Customer 1
Supplier A ── Customer 2
Supplier A ── Customer 3
Supplier B ── Customer 1
Supplier B ── Customer 2
...
```

PriceSync changes the topology to:

```text
Supplier systems ──► PriceSync ──► Customer systems
```

Each participant should ideally need to solve integration with PriceSync once rather than maintain separate price-distribution workflows for every trading partner.

## What PriceSync is

- A technical integration hub for B2B prices
- A neutral price-distribution network
- A canonical model for customer-specific B2B prices
- A synchronization and versioning layer
- An authorization boundary between supplier and customer
- A transformation boundary between external file/API formats and the canonical model
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

The user may never need to visit PriceSync directly. Native integrations should be able to make PriceSync almost invisible infrastructure.

## Adoption principle

Connecting should be easier than continuing to exchange price lists manually.

### Zero-integration onboarding

Suppliers and customers should be able to participate without changing their existing systems or file formats.

A supplier that already exports an Excel or CSV price list should be able to upload that same file to PriceSync. During first-time onboarding, the supplier maps existing columns to the minimal PriceSync model, for example:

```text
Supplier Excel column         PriceSync field
------------------------------------------------
Art.nr                    ->  ArticleId
Nettopris                 ->  Price
Enhet                     ->  PriceUnit
Valuta                    ->  Currency
Gäller fr.o.m.            ->  ValidFrom
Benämning                 ->  Ignore
Produktgrupp              ->  Ignore
```

The mapping is saved as an import profile and reused for future files. Subsequent uploads using the same layout should require little or no manual work.

An import profile is conceptually a first-class resource:

```text
ImportProfile
├── SupplierId
├── FileType
├── Sheet / header configuration
├── Column mappings
└── Default values
```

The same profile should eventually be usable for manual upload, scheduled file delivery, SFTP or other ingestion channels.

This allows a supplier to replace:

```text
ERP export -> Excel -> email to many customers
```

with:

```text
ERP export -> Excel -> PriceSync -> many customers
```

without first building an API integration.

### Customer-side compatibility

Customers should also be able to consume prices without a native integration. A canonical PriceSet may be exposed as:

- PriceSync API
- CSV
- Excel
- customer-specific export formats where justified

This means PriceSync can act as a transformation layer on both sides:

```text
Supplier format
      |
  import adapter
      |
      v
Canonical PriceSet
      |
  export adapter
      |
      v
Customer format
```

Supplier- and customer-specific formats should stay at the platform boundary. The canonical model should remain independent of individual ERP/file layouts.

A customer may therefore adopt PriceSync before its ERP or estimation system has native support.

### Progressive automation

File-based onboarding is an adoption mechanism, not the architectural core. PriceSync should remain API-first internally while allowing participants to automate gradually:

```text
Manual Excel/CSV upload
       ↓
Scheduled file delivery / SFTP
       ↓
REST API
       ↓
Native ERP connector
```

All paths should result in the same canonical PriceSync representation.

The core principle is:

> Zero-integration onboarding: both suppliers and customers should be able to participate using the files and workflows they already have.

## Initial vertical hypothesis

Construction-related suppliers and buyers, including house manufacturers, are a candidate initial market because price-list handling appears highly fragmented and customer-specific pricing is common.

This is a hypothesis to validate, not a final market restriction.
