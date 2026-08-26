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
  import mapping
      |
      v
Canonical PriceSet
      |
  export mapping
      |
      v
Customer format
```

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
