
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InvoiceIQ.DataGenerator.Models;

namespace InvoiceIQ.DataGenerator.Exports;

public class DatasetExporter
{
    private const int Seed = 20261022;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public void Export(
        GeneratedDataset dataset,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        ExportCsv(dataset, outputDirectory);
        ExportJson(dataset, outputDirectory);
        ExportSql(dataset, outputDirectory);

        WriteValidationSummary(dataset, outputDirectory);
        WriteDataDictionary(outputDirectory);
        WriteReadme(outputDirectory);
        WriteSourceManifest(dataset, outputDirectory);

        // Generate checksums last so the manifest is included.
        WriteChecksums(outputDirectory);

        Console.WriteLine();
        Console.WriteLine("Dataset export completed.");
        Console.WriteLine($"Output directory: {outputDirectory}");
    }

    // =========================================================
    // CSV EXPORT
    // =========================================================

    private void ExportCsv(
        GeneratedDataset dataset,
        string outputDirectory)
    {
        string csvDirectory = Path.Combine(
            outputDirectory,
            "csv");

        Directory.CreateDirectory(csvDirectory);

        WriteCsv(Path.Combine(csvDirectory, "vendors.csv"),
            dataset.Vendors);

        WriteCsv(Path.Combine(csvDirectory, "purchase_orders.csv"),
            dataset.PurchaseOrders);

        WriteCsv(Path.Combine(csvDirectory, "purchase_order_lines.csv"),
            dataset.PurchaseOrderLines);

        WriteCsv(Path.Combine(csvDirectory, "goods_receipts.csv"),
            dataset.GoodsReceipts);

        WriteCsv(Path.Combine(csvDirectory, "goods_receipt_lines.csv"),
            dataset.GoodsReceiptLines);

        WriteCsv(Path.Combine(csvDirectory, "invoices.csv"),
            dataset.Invoices);

        WriteCsv(Path.Combine(csvDirectory, "invoice_lines.csv"),
            dataset.InvoiceLines);

        WriteCsv(Path.Combine(csvDirectory, "ground_truth.csv"),
            dataset.GroundTruth);
    }

    private void WriteCsv<T>(
        string path,
        IEnumerable<T> records)
    {
        var properties = typeof(T).GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        var builder = new StringBuilder();

        builder.AppendLine(string.Join(",",
            properties.Select(x => EscapeCsv(x.Name))));

        foreach (var record in records)
        {
            var values = properties.Select(property =>
            {
                object? value = property.GetValue(record);

                return EscapeCsv(FormatValue(value));
            });

            builder.AppendLine(string.Join(",", values));
        }

        File.WriteAllText(
            path,
            builder.ToString(),
            new UTF8Encoding(true));
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;

        if (value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\n') ||
            value.Contains('\r'))
        {
            return "\"" +
                   value.Replace("\"", "\"\"") +
                   "\"";
        }

        return value;
    }

    private static string FormatValue(object? value)
    {
        if (value == null)
            return string.Empty;

        return value switch
        {
            DateTime date =>
                date.ToString("O", CultureInfo.InvariantCulture),

            decimal number =>
                number.ToString(CultureInfo.InvariantCulture),

            bool boolean =>
                boolean ? "true" : "false",

            IFormattable formattable =>
                formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture) ?? string.Empty,

            _ => value.ToString() ?? string.Empty
        };
    }

    // =========================================================
    // JSON EXPORT
    // =========================================================

    private void ExportJson(
        GeneratedDataset dataset,
        string outputDirectory)
    {
        // Operational records contain no ground-truth labels.
        var operationalDataset = new
        {
            dataset.Vendors,
            dataset.PurchaseOrders,
            dataset.PurchaseOrderLines,
            dataset.GoodsReceipts,
            dataset.GoodsReceiptLines,
            dataset.Invoices,
            dataset.InvoiceLines
        };

        string operationalJson = JsonSerializer.Serialize(
            operationalDataset,
            _jsonOptions);

        File.WriteAllText(
            Path.Combine(outputDirectory, "complete_dataset.json"),
            operationalJson,
            new UTF8Encoding(false));

        // Ground truth is exported independently for evaluation.
        string groundTruthJson = JsonSerializer.Serialize(
            dataset.GroundTruth,
            _jsonOptions);

        File.WriteAllText(
            Path.Combine(outputDirectory, "ground_truth.json"),
            groundTruthJson,
            new UTF8Encoding(false));
    }

    // =========================================================
    // POSTGRESQL STAGING SQL EXPORT
    // =========================================================

    private void ExportSql(
        GeneratedDataset dataset,
        string outputDirectory)
    {
        var builder = new StringBuilder();

        builder.AppendLine("-- InvoiceIQ generated dataset");
        builder.AppendLine("-- PostgreSQL staging export");
        builder.AppendLine($"-- Seed: {Seed}");
        builder.AppendLine();

        builder.AppendLine(
            "CREATE SCHEMA IF NOT EXISTS invoiceiq_staging;");

        builder.AppendLine();

        builder.AppendLine("""
            CREATE TABLE IF NOT EXISTS invoiceiq_staging.generated_records
            (
                entity_type VARCHAR(100) NOT NULL,
                record_id VARCHAR(200) NOT NULL,
                payload JSONB NOT NULL,
                PRIMARY KEY (entity_type, record_id)
            );
            """);

        builder.AppendLine();
        builder.AppendLine("BEGIN;");
        builder.AppendLine();

        AppendSqlRecords(
            builder,
            "Vendor",
            dataset.Vendors,
            x => x.VendorId);

        AppendSqlRecords(
            builder,
            "PurchaseOrder",
            dataset.PurchaseOrders,
            x => x.PurchaseOrderId);

        AppendSqlRecords(
            builder,
            "PurchaseOrderLine",
            dataset.PurchaseOrderLines,
            x => x.PurchaseOrderLineId);

        AppendSqlRecords(
            builder,
            "GoodsReceipt",
            dataset.GoodsReceipts,
            x => x.GoodsReceiptId);

        AppendSqlRecords(
            builder,
            "GoodsReceiptLine",
            dataset.GoodsReceiptLines,
            x => x.GoodsReceiptLineId);

        AppendSqlRecords(
            builder,
            "Invoice",
            dataset.Invoices,
            x => x.InvoiceId);

        AppendSqlRecords(
            builder,
            "InvoiceLine",
            dataset.InvoiceLines,
            x => x.InvoiceLineId);

        AppendSqlRecords(
            builder,
            "GroundTruth",
            dataset.GroundTruth,
            x => x.InvoiceId);

        builder.AppendLine();
        builder.AppendLine("COMMIT;");

        File.WriteAllText(
            Path.Combine(outputDirectory, "dataset.sql"),
            builder.ToString(),
            new UTF8Encoding(false));
    }

    private void AppendSqlRecords<T>(
        StringBuilder builder,
        string entityType,
        IEnumerable<T> records,
        Func<T, string> getId)
    {
        foreach (var record in records)
        {
            string recordId = EscapeSql(getId(record));

            string json = JsonSerializer.Serialize(
                record,
                _jsonOptions);

            string escapedJson = EscapeSql(json);

            builder.AppendLine(
                "INSERT INTO invoiceiq_staging.generated_records " +
                "(entity_type, record_id, payload) VALUES " +
                $"('{EscapeSql(entityType)}', " +
                $"'{recordId}', '{escapedJson}'::jsonb) " +
                "ON CONFLICT (entity_type, record_id) " +
                "DO UPDATE SET payload = EXCLUDED.payload;");
        }
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("'", "''");
    }

    // =========================================================
    // VALIDATION SUMMARY
    // =========================================================

    private void WriteValidationSummary(
        GeneratedDataset dataset,
        string outputDirectory)
    {
        var vendorIds = dataset.Vendors
            .Select(x => x.VendorId)
            .ToHashSet();

        var poIds = dataset.PurchaseOrders
            .Select(x => x.PurchaseOrderId)
            .ToHashSet();

        var poLineIds = dataset.PurchaseOrderLines
            .Select(x => x.PurchaseOrderLineId)
            .ToHashSet();

        var grnIds = dataset.GoodsReceipts
            .Select(x => x.GoodsReceiptId)
            .ToHashSet();

        var invoiceIds = dataset.Invoices
            .Select(x => x.InvoiceId)
            .ToHashSet();

        int invalidPoVendorReferences =
            dataset.PurchaseOrders.Count(
                x => !vendorIds.Contains(x.VendorId));

        int invalidPoLineReferences =
            dataset.PurchaseOrderLines.Count(
                x => !poIds.Contains(x.PurchaseOrderId));

        int invalidGrnReferences =
            dataset.GoodsReceipts.Count(
                x => !poIds.Contains(x.PurchaseOrderId));

        int invalidGrnLineReferences =
            dataset.GoodsReceiptLines.Count(
                x => !grnIds.Contains(x.GoodsReceiptId) ||
                     !poLineIds.Contains(x.PurchaseOrderLineId));

        int invalidInvoiceReferences =
            dataset.Invoices.Count(
                x => !vendorIds.Contains(x.VendorId) ||
                     x.PurchaseOrderId == null ||
                     !poIds.Contains(x.PurchaseOrderId));

        int invalidInvoiceLineReferences =
            dataset.InvoiceLines.Count(
                x => !invoiceIds.Contains(x.InvoiceId));

        int invalidGroundTruthReferences =
            dataset.GroundTruth.Count(
                x => !invoiceIds.Contains(x.InvoiceId));

        int duplicateInvoiceNumbers =
            dataset.Invoices
                .GroupBy(x => new
                {
                    x.VendorId,
                    x.InvoiceNumber
                })
                .Sum(group => Math.Max(0, group.Count() - 1));

        int invalidInvoiceTotals = 0;
        int invoicesWithoutLines = 0;

        foreach (var invoice in dataset.Invoices)
        {
            var lines = dataset.InvoiceLines
                .Where(x => x.InvoiceId == invoice.InvoiceId)
                .ToList();

            if (lines.Count == 0)
            {
                invoicesWithoutLines++;
                invalidInvoiceTotals++;
                continue;
            }

            decimal subtotal = lines.Sum(
                x => RoundMoney(x.Quantity * x.UnitPrice));

            decimal tax = lines.Sum(
                x => RoundMoney(
                    RoundMoney(x.Quantity * x.UnitPrice) *
                    x.TaxRate));

            if (invoice.Subtotal != subtotal ||
                invoice.TaxAmount != tax ||
                invoice.GrandTotal != subtotal + tax)
            {
                invalidInvoiceTotals++;
            }
        }

        var scenarioCounts = dataset.GroundTruth
            .GroupBy(x => x.ScenarioCode)
            .OrderBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.Count());

        bool allReferencesPassed =
            invalidPoVendorReferences == 0 &&
            invalidPoLineReferences == 0 &&
            invalidGrnReferences == 0 &&
            invalidGrnLineReferences == 0 &&
            invalidInvoiceReferences == 0 &&
            invalidInvoiceLineReferences == 0 &&
            invalidGroundTruthReferences == 0;

        var report = new
        {
            Seed,

            RecordCounts = new
            {
                Vendors = dataset.Vendors.Count,
                PurchaseOrders = dataset.PurchaseOrders.Count,
                PurchaseOrderLines = dataset.PurchaseOrderLines.Count,
                GoodsReceipts = dataset.GoodsReceipts.Count,
                GoodsReceiptLines = dataset.GoodsReceiptLines.Count,
                Invoices = dataset.Invoices.Count,
                InvoiceLines = dataset.InvoiceLines.Count,
                GroundTruth = dataset.GroundTruth.Count
            },

            ScenarioCounts = scenarioCounts,

            ReferentialIntegrity = new
            {
                InvalidPoVendorReferences = invalidPoVendorReferences,
                InvalidPoLineReferences = invalidPoLineReferences,
                InvalidGrnReferences = invalidGrnReferences,
                InvalidGrnLineReferences = invalidGrnLineReferences,
                InvalidInvoiceReferences = invalidInvoiceReferences,
                InvalidInvoiceLineReferences = invalidInvoiceLineReferences,
                InvalidGroundTruthReferences = invalidGroundTruthReferences
            },

            BusinessValidation = new
            {
                InvalidInvoiceTotals = invalidInvoiceTotals,
                InvoicesWithoutLines = invoicesWithoutLines,
                DuplicateInvoiceNumbers = duplicateInvoiceNumbers
            },

            AllReferenceChecksPassed = allReferencesPassed,
            AllInvoiceTotalsPassed = invalidInvoiceTotals == 0
        };

        string json = JsonSerializer.Serialize(
            report,
            _jsonOptions);

        File.WriteAllText(
            Path.Combine(outputDirectory, "validation_summary.json"),
            json,
            new UTF8Encoding(false));
    }

    // =========================================================
    // DATA DICTIONARY
    // =========================================================

    private static void WriteDataDictionary(
        string outputDirectory)
    {
        string content = """
        # InvoiceIQ Data Dictionary

        ## vendors.csv
        - VendorId: Unique vendor identifier.
        - VendorCode: Business vendor code.
        - VendorName: Vendor name.
        - Email: Vendor accounts email.
        - Currency: Vendor transaction currency.
        - Country: Vendor country.
        - IsActive: Vendor active status.
        - EffectiveFrom: Vendor effective start date.
        - EffectiveTo: Optional vendor effective end date.

        ## purchase_orders.csv
        - PurchaseOrderId: Unique PO identifier.
        - PurchaseOrderNumber: Business PO number.
        - VendorId: Vendor foreign key.
        - OrderDate: Purchase order date.
        - DeliveryDate: Expected delivery date.
        - Currency: PO currency.
        - TotalAmount: Calculated PO total.
        - Status: PO lifecycle status.
        - EffectiveFrom: PO effective start date.
        - EffectiveTo: Optional PO effective end date.

        ## purchase_order_lines.csv
        - PurchaseOrderLineId: Unique PO line identifier.
        - PurchaseOrderId: Parent PO identifier.
        - LineReference: PO line number.
        - ProductCode: Product identifier.
        - Description: Product description.
        - OrderedQuantity: Ordered quantity.
        - UnitPrice: Agreed unit price.
        - TaxRate: Tax percentage represented as a decimal.
        - TaxCode: Tax category.
        - LineAmount: Calculated line total including tax.

        ## goods_receipts.csv
        - GoodsReceiptId: Unique goods receipt identifier.
        - GoodsReceiptNumber: Business goods receipt number.
        - PurchaseOrderId: Parent PO identifier.
        - ReceiptDate: Receipt date.
        - Status: Receipt lifecycle status.

        ## goods_receipt_lines.csv
        - GoodsReceiptLineId: Unique receipt line identifier.
        - GoodsReceiptId: Parent goods receipt.
        - PurchaseOrderLineId: Referenced PO line.
        - ReceivedQuantity: Quantity received.

        ## invoices.csv
        - InvoiceId: Unique invoice record identifier.
        - InvoiceNumber: Supplier invoice number.
        - VendorId: Vendor foreign key.
        - PurchaseOrderId: Related PO identifier.
        - InvoiceDate: Invoice date.
        - DueDate: Payment due date.
        - Currency: Invoice currency.
        - Subtotal: Calculated invoice subtotal.
        - TaxAmount: Calculated invoice tax.
        - GrandTotal: Calculated invoice total.
        - Status: Generated initial status.
        - CreatedAt: Invoice creation timestamp.

        ## invoice_lines.csv
        - InvoiceLineId: Unique invoice line identifier.
        - InvoiceId: Parent invoice.
        - LineReference: Invoice line number.
        - ProductCode: Product identifier.
        - Description: Product description.
        - Quantity: Invoiced quantity.
        - UnitPrice: Invoiced unit price.
        - TaxRate: Invoice tax rate.
        - TaxCode: Invoice tax code.
        - LineAmount: Calculated line total including tax.

        ## ground_truth.csv / ground_truth.json
        Ground-truth labels are evaluation targets, not model features.
        - InvoiceId: Invoice identifier.
        - ScenarioCode: Injected anomaly scenario.
        - ExpectedOutcome: Expected matching outcome.
        - ExpectedExceptionType: Expected exception category.
        - IsDuplicate: Duplicate invoice label.
        - HasPriceVariance: Price variance label.
        - HasQuantityMismatch: Quantity mismatch label.
        - HasMissingReceipt: Missing receipt label.
        - HasTaxOrVendorError: Tax/vendor error label.
        - IsEligibleForApproval: Expected approval eligibility.
        - IsEligibleForPayment: Expected payment eligibility.
        - Explanation: Scenario explanation.

        IMPORTANT:
        Never include scenario labels or ground-truth columns
        in model input features.
        """;

        File.WriteAllText(
            Path.Combine(outputDirectory, "DATA_DICTIONARY.md"),
            content,
            new UTF8Encoding(false));
    }

    // =========================================================
    // README
    // =========================================================

    private static void WriteReadme(
        string outputDirectory)
    {
        string content = """
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
        """;

        File.WriteAllText(
            Path.Combine(outputDirectory, "README.md"),
            content,
            new UTF8Encoding(false));
    }

    // =========================================================
    // SOURCE MANIFEST
    // =========================================================

    private void WriteSourceManifest(
        GeneratedDataset dataset,
        string outputDirectory)
    {
        // No generation timestamp: the manifest remains reproducible.
        var manifest = new
        {
            Generator = "InvoiceIQ.DataGenerator",
            Version = "1.0.0",
            Seed,

            RecordCounts = new
            {
                Vendors = dataset.Vendors.Count,
                PurchaseOrders = dataset.PurchaseOrders.Count,
                PurchaseOrderLines = dataset.PurchaseOrderLines.Count,
                GoodsReceipts = dataset.GoodsReceipts.Count,
                GoodsReceiptLines = dataset.GoodsReceiptLines.Count,
                Invoices = dataset.Invoices.Count,
                InvoiceLines = dataset.InvoiceLines.Count,
                GroundTruth = dataset.GroundTruth.Count
            },

            ExportFormats = new[]
            {
                "CSV",
                "JSON",
                "PostgreSQL staging SQL"
            }
        };

        File.WriteAllText(
            Path.Combine(outputDirectory, "source_manifest.json"),
            JsonSerializer.Serialize(manifest, _jsonOptions),
            new UTF8Encoding(false));
    }

    // =========================================================
    // CHECKSUMS
    // =========================================================

    private static void WriteChecksums(
        string outputDirectory)
    {
        var files = Directory
            .GetFiles(
                outputDirectory,
                "*",
                SearchOption.AllDirectories)
            .Where(path =>
                !Path.GetFileName(path).Equals(
                    "checksums.sha256",
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(
                path => Path.GetRelativePath(outputDirectory, path),
                StringComparer.Ordinal)
            .ToList();

        var lines = new List<string>();

        foreach (var file in files)
        {
            using var stream = File.OpenRead(file);

            byte[] hash = SHA256.HashData(stream);

            string hashText = Convert.ToHexString(hash)
                .ToLowerInvariant();

            string relativePath = Path
                .GetRelativePath(outputDirectory, file)
                .Replace('\\', '/');

            lines.Add($"{hashText}  {relativePath}");
        }

        File.WriteAllLines(
            Path.Combine(outputDirectory, "checksums.sha256"),
            lines,
            new UTF8Encoding(false));
    }

    // =========================================================
    // FINANCIAL HELPERS
    // =========================================================

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);
    }
}
