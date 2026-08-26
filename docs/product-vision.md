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

PriceSync should also provide useful standalone value before a trading partner is connected. Network effects should amplify existing value rather than be required to create value.

### Zero-integration onboarding

Suppliers and customers should be able to participate without changing their existing systems or file formats.

A supplier that already exports an Excel or CSV price list should be able to upload that same file to PriceSync.

The onboarding experience should optimize for:

```text
Existing supplier Excel
        ↓
AI-assisted mapping
        ↓
Human confirmation
        ↓
Saved deterministic ImportProfile
        ↓
Normalized PriceSync prices
        ↓
Excel / CSV / API / future distribution
```

The supplier should not need to understand the PriceSync canonical model before receiving value.

### AI-assisted mapping as supplier value

AI-assisted mapping is a candidate acquisition and onboarding feature, especially on the supplier side where standalone network value is otherwise weaker.

Given an existing spreadsheet such as:

```text
Artikelnr | Benämning | Enh | Netto | Rabattgr | Fr.o.m
100234    | Regel...  | M   | 12,84 | 120      | 260901
```

PriceSync may propose:

```text
Supplier Excel column         PriceSync field      Confidence
--------------------------------------------------------------
Artikelnr                 ->  ArticleId             99%
Netto                     ->  Price                 99%
Enh                       ->  PriceUnit             96%
Fr.o.m                    ->  ValidFrom             94%
Currency                  ->  constant: SEK
Benämning                 ->  Ignore
Rabattgr                  ->  Ignore
```

The user confirms or corrects the mapping once. PriceSync then stores a deterministic import profile and reuses it for subsequent imports.

AI should primarily assist with discovering and repairing mappings. It should not freely reinterpret every production import. Routine imports should be deterministic and validation-driven.

If a source format changes, PriceSync can detect that the saved profile no longer matches and propose a repair, for example:

```text
Expected column "Netto" was not found.
Possible replacement: "Nettopris".
Review mapping?
```

This creates supplier-side standalone value even before a customer relationship exists:

> Existing Excel export → standardized, validated price data → normalized Excel/CSV/API.

A supplier may therefore join PriceSync simply to turn an existing spreadsheet export into reusable machine-readable price data. When customers later connect, the already-normalized data can be distributed without a second onboarding project.

### Import profiles

A confirmed mapping is saved as an import profile and reused for future files. Subsequent uploads using the same layout should require little or no manual work.

An import profile is conceptually a first-class resource:

```text
ImportProfile
├── OrganizationId
├── FileType
├── Sheet / header configuration
├── Column mappings
├── Parsing rules
└── Default values
```

The same profile should eventually be usable for manual upload, scheduled file delivery, SFTP or other ingestion channels.

This allows a supplier to progress from:

```text
ERP export -> Excel -> manual PriceSync upload
```

through:

```text
ERP export -> Excel -> automated file delivery -> PriceSync
```

and eventually to:

```text
ERP -> PriceSync API
```

without changing the canonical semantics of the normalized prices.

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

> Zero-integration onboarding: participants should be able to start with the files and workflows they already have, and PriceSync should turn those into normalized price data with minimal manual configuration.

## Initial vertical hypothesis

Construction-related suppliers and buyers, including house manufacturers, are a candidate initial market because price-list handling appears highly fragmented and customer-specific pricing is common.

This is a hypothesis to validate, not a final market restriction.
