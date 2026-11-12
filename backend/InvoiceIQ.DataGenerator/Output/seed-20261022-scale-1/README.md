# InvoiceIQ Generated Dataset

## Purpose
Synthetic procurement and invoice dataset for testing invoice
intake, extraction, matching, exception routing and payment readiness.

## Reproducibility
Random seed: 20261022.

The generator uses a fixed seed. Identical source code and
configuration should produce identical business records.

## Outputs
- csv/: Individual entity CSV exports.
- complete_dataset.json: Operational dataset without ground truth.
- ground_truth.json: Separate evaluation labels.
- dataset.sql: PostgreSQL staging SQL.
- validation_summary.json: Record counts and validation results.
- DATA_DICTIONARY.md: Field definitions.
- checksums.sha256: SHA-256 hashes of exported files.
- source_manifest.json: Generation metadata.

## Ground truth
Ground truth is stored separately from business records.
Do not use scenario labels or expected outcomes as model input.

## SQL
dataset.sql creates invoiceiq_staging.generated_records.
It is a staging format, not a direct insert script for the
InvoiceIQ application tables.

## Base scenario distribution (scale 1)
- CLEAN: 1,400
- PRICE_VARIANCE: 200
- QUANTITY_MISMATCH: 160
- DUPLICATE_INVOICE: 100
- MISSING_RECEIPT: 80
- TAX_VENDOR_ERROR: 60

## Important
This is synthetic test data. Vendor names and invoice records
do not represent real businesses or transactions.