
using InvoiceIQ.DataGenerator.Models;

namespace InvoiceIQ.DataGenerator.Services;

public class InvoiceGenerator
{
    private const int Seed = 20261022;

    private readonly int _scale;
    private readonly Random _random = new(Seed);

    private int InvoiceCount => 2000 * _scale;

    private Dictionary<string, int> ScenarioCounts =>
        new()
        {
            ["CLEAN"] = 1400 * _scale,
            ["PRICE_VARIANCE"] = 200 * _scale,
            ["QUANTITY_MISMATCH"] = 160 * _scale,
            ["DUPLICATE_INVOICE"] = 100 * _scale,
            ["MISSING_RECEIPT"] = 80 * _scale,
            ["TAX_VENDOR_ERROR"] = 60 * _scale
        };

    public InvoiceGenerator(int scale = 1)
    {
        if (scale < 1 || scale > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scale),
                "Scale must be between 1 and 100.");
        }

        _scale = scale;
    }

    public void Generate(GeneratedDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        if (dataset.Vendors.Count == 0 ||
            dataset.PurchaseOrders.Count == 0 ||
            dataset.PurchaseOrderLines.Count == 0)
        {
            throw new InvalidOperationException(
                "Generate master data before generating invoices.");
        }

        if (dataset.Invoices.Count > 0 ||
            dataset.InvoiceLines.Count > 0 ||
            dataset.GroundTruth.Count > 0)
        {
            throw new InvalidOperationException(
                "Invoice generation requires an empty invoice dataset.");
        }

        var scenarios = BuildScenarioList();

        // Generate original invoices first.
        // Duplicate invoices are generated separately at the end.
        var originalScenarios = scenarios
            .Where(x => x != "DUPLICATE_INVOICE")
            .ToList();

        Shuffle(originalScenarios);

        int invoiceIndex = 0;
        int taxVendorErrorIndex = 0;

        // Every original invoice gets a different PO line.
        // This prevents separate invoices from unintentionally
        // interfering with each other's quantity matching.
        var usedPoLines = new HashSet<string>();

        foreach (string scenario in originalScenarios)
        {
            invoiceIndex++;

            var poLine = SelectPurchaseOrderLine(
                dataset,
                scenario,
                usedPoLines);

            var purchaseOrder = dataset.PurchaseOrders.Single(
                x => x.PurchaseOrderId == poLine.PurchaseOrderId);

            var poVendor = dataset.Vendors.Single(
                x => x.VendorId == purchaseOrder.VendorId);

            bool injectVendorError = false;
            bool injectTaxError = false;

            if (scenario == "TAX_VENDOR_ERROR")
            {
                // Exactly 30 vendor errors and 30 tax errors
                // at scale 1.
                injectVendorError = taxVendorErrorIndex % 2 == 0;
                injectTaxError = !injectVendorError;

                taxVendorErrorIndex++;
            }

            var invoiceVendor = injectVendorError
                ? SelectDifferentVendor(dataset, poVendor.VendorId)
                : poVendor;

            CreateInvoice(
                dataset,
                poLine,
                purchaseOrder,
                invoiceVendor,
                scenario,
                invoiceIndex,
                injectTaxError);
        }

        // Generate duplicates only from clean original invoices.
        // This avoids duplicate records inheriting other anomalies.
        int duplicateCount = ScenarioCounts["DUPLICATE_INVOICE"];

        var cleanInvoiceIds = dataset.GroundTruth
            .Where(x => x.ScenarioCode == "CLEAN")
            .Select(x => x.InvoiceId)
            .ToHashSet();

        var duplicateSources = dataset.Invoices
            .Where(x => cleanInvoiceIds.Contains(x.InvoiceId))
            .ToList();

        if (duplicateSources.Count < duplicateCount)
        {
            throw new InvalidOperationException(
                "Not enough clean invoices to create duplicate scenarios.");
        }

        for (int i = 0; i < duplicateCount; i++)
        {
            invoiceIndex++;

            CreateDuplicateInvoice(
                dataset,
                invoiceIndex,
                duplicateSources[i]);
        }

        ValidateInvoices(dataset);
    }

    // =========================================================
    // SCENARIO DISTRIBUTION
    // =========================================================

    private List<string> BuildScenarioList()
    {
        var scenarios = new List<string>();

        foreach (var scenario in ScenarioCounts)
        {
            scenarios.AddRange(
                Enumerable.Repeat(
                    scenario.Key,
                    scenario.Value));
        }

        if (scenarios.Count != InvoiceCount)
        {
            throw new InvalidOperationException(
                "Scenario distribution does not total the expected invoice count.");
        }

        return scenarios;
    }

    private void Shuffle(List<string> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);

            (values[i], values[j]) =
                (values[j], values[i]);
        }
    }

    // =========================================================
    // PURCHASE ORDER LINE SELECTION
    // =========================================================

    private PurchaseOrderLine SelectPurchaseOrderLine(
        GeneratedDataset dataset,
        string scenario,
        HashSet<string> usedPoLines)
    {
        var candidates = dataset.PurchaseOrderLines
            .Where(line =>
            {
                bool hasReceipt = dataset.GoodsReceipts.Any(
                    gr => gr.PurchaseOrderId == line.PurchaseOrderId);

                return scenario == "MISSING_RECEIPT"
                    ? !hasReceipt
                    : hasReceipt;
            })
            .Where(line =>
                !usedPoLines.Contains(line.PurchaseOrderLineId))
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No unused eligible PO line found for scenario {scenario}.");
        }

        var selected = candidates[
            _random.Next(candidates.Count)];

        usedPoLines.Add(selected.PurchaseOrderLineId);

        return selected;
    }

    private Vendor SelectDifferentVendor(
        GeneratedDataset dataset,
        string excludedVendorId)
    {
        var vendors = dataset.Vendors
            .Where(x =>
                x.VendorId != excludedVendorId &&
                x.IsActive)
            .ToList();

        if (vendors.Count == 0)
        {
            throw new InvalidOperationException(
                "No alternative active vendor is available.");
        }

        return vendors[_random.Next(vendors.Count)];
    }

    // =========================================================
    // CREATE ORIGINAL INVOICE
    // =========================================================

    private void CreateInvoice(
        GeneratedDataset dataset,
        PurchaseOrderLine poLine,
        PurchaseOrder purchaseOrder,
        Vendor invoiceVendor,
        string scenario,
        int invoiceIndex,
        bool injectTaxError)
    {
        decimal quantity = scenario == "QUANTITY_MISMATCH"
            ? poLine.OrderedQuantity + 1m
            : CalculateNormalQuantity(poLine);

        decimal unitPrice = poLine.UnitPrice;
        decimal taxRate = poLine.TaxRate;
        string taxCode = poLine.TaxCode;

        if (scenario == "PRICE_VARIANCE")
        {
            unitPrice = RoundMoney(unitPrice * 1.15m);
        }

        if (injectTaxError)
        {
            taxCode = "GST_ERROR";
        }

        decimal subtotal = RoundMoney(quantity * unitPrice);
        decimal taxAmount = RoundMoney(subtotal * taxRate);
        decimal grandTotal = subtotal + taxAmount;

        string invoiceId =
            $"INV-{invoiceVendor.VendorCode}-2026-{invoiceIndex:D6}";

        string invoiceNumber =
            $"INV-{invoiceVendor.VendorCode}-2026-{invoiceIndex:D6}";

        DateTime invoiceDate =
            purchaseOrder.OrderDate.AddDays(30);

        var invoice = new Invoice
        {
            InvoiceId = invoiceId,
            InvoiceNumber = invoiceNumber,
            VendorId = invoiceVendor.VendorId,
            PurchaseOrderId = purchaseOrder.PurchaseOrderId,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(30),
            Currency = invoiceVendor.Currency,
            Subtotal = subtotal,
            TaxAmount = taxAmount,
            GrandTotal = grandTotal,
            Status = scenario == "CLEAN"
                ? "RECEIVED"
                : "EXCEPTION",
            CreatedAt = invoiceDate
        };

        var invoiceLine = new InvoiceLine
        {
            InvoiceLineId = $"INVL-{invoiceId}-01",
            InvoiceId = invoiceId,
            LineReference = "01",
            ProductCode = poLine.ProductCode,
            Description = poLine.Description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxRate = taxRate,
            TaxCode = taxCode,
            LineAmount = grandTotal
        };

        dataset.Invoices.Add(invoice);
        dataset.InvoiceLines.Add(invoiceLine);

        dataset.GroundTruth.Add(
            CreateGroundTruth(invoice, scenario));
    }

    // =========================================================
    // DUPLICATE INVOICE GENERATION
    // =========================================================

    private void CreateDuplicateInvoice(
        GeneratedDataset dataset,
        int invoiceIndex,
        Invoice original)
    {
        var originalLine = dataset.InvoiceLines.Single(
            x => x.InvoiceId == original.InvoiceId);

        var vendor = dataset.Vendors.Single(
            x => x.VendorId == original.VendorId);

        string duplicateId =
            $"INV-{vendor.VendorCode}-2026-{invoiceIndex:D6}";

        var duplicate = new Invoice
        {
            InvoiceId = duplicateId,

            // Same vendor and invoice number:
            // intentionally creates a duplicate.
            InvoiceNumber = original.InvoiceNumber,

            VendorId = original.VendorId,
            PurchaseOrderId = original.PurchaseOrderId,
            InvoiceDate = original.InvoiceDate,
            DueDate = original.DueDate,
            Currency = original.Currency,
            Subtotal = original.Subtotal,
            TaxAmount = original.TaxAmount,
            GrandTotal = original.GrandTotal,
            Status = "EXCEPTION",
            CreatedAt = original.CreatedAt
        };

        var duplicateLine = new InvoiceLine
        {
            InvoiceLineId = $"INVL-{duplicateId}-01",
            InvoiceId = duplicateId,
            LineReference = originalLine.LineReference,
            ProductCode = originalLine.ProductCode,
            Description = originalLine.Description,
            Quantity = originalLine.Quantity,
            UnitPrice = originalLine.UnitPrice,
            TaxRate = originalLine.TaxRate,
            TaxCode = originalLine.TaxCode,
            LineAmount = originalLine.LineAmount
        };

        dataset.Invoices.Add(duplicate);
        dataset.InvoiceLines.Add(duplicateLine);

        dataset.GroundTruth.Add(
            CreateGroundTruth(
                duplicate,
                "DUPLICATE_INVOICE"));
    }

    // =========================================================
    // QUANTITY AND FINANCIAL CALCULATIONS
    // =========================================================

    private decimal CalculateNormalQuantity(
        PurchaseOrderLine poLine)
    {
        // Use a conservative quantity for normal invoices.
        // The selected PO line has sufficient receipt quantity.
        decimal quantity = Math.Round(
            poLine.OrderedQuantity * 0.10m,
            3,
            MidpointRounding.AwayFromZero);

        return Math.Max(0.001m, quantity);
    }

    private static decimal RoundMoney(decimal amount)
    {
        return Math.Round(
            amount,
            2,
            MidpointRounding.AwayFromZero);
    }

    // =========================================================
    // GROUND TRUTH
    // =========================================================

    private static GroundTruth CreateGroundTruth(
        Invoice invoice,
        string scenario)
    {
        bool clean = scenario == "CLEAN";

        bool duplicate =
            scenario == "DUPLICATE_INVOICE";

        bool priceVariance =
            scenario == "PRICE_VARIANCE";

        bool quantityMismatch =
            scenario == "QUANTITY_MISMATCH";

        bool missingReceipt =
            scenario == "MISSING_RECEIPT";

        bool taxOrVendorError =
            scenario == "TAX_VENDOR_ERROR";

        string exceptionType = scenario switch
        {
            "PRICE_VARIANCE" => "PRICE_VARIANCE",
            "QUANTITY_MISMATCH" => "QUANTITY_MISMATCH",
            "DUPLICATE_INVOICE" => "DUPLICATE_INVOICE",
            "MISSING_RECEIPT" => "MISSING_RECEIPT",
            "TAX_VENDOR_ERROR" => "TAX_OR_VENDOR_MISMATCH",
            _ => string.Empty
        };

        string explanation = scenario switch
        {
            "CLEAN" =>
                "Invoice uses the correct vendor, PO line, price, tax and a received quantity within the PO and receipt limits.",

            "PRICE_VARIANCE" =>
                "Invoice unit price is deliberately 15% higher than the purchase order.",

            "QUANTITY_MISMATCH" =>
                "Invoice quantity deliberately exceeds the ordered quantity.",

            "DUPLICATE_INVOICE" =>
                "Invoice number duplicates an existing invoice number for the same vendor.",

            "MISSING_RECEIPT" =>
                "The linked purchase order has no goods receipt.",

            "TAX_VENDOR_ERROR" =>
                "Invoice contains a deliberate tax-code or vendor mismatch.",

            _ => "Unknown scenario."
        };

        return new GroundTruth
        {
            InvoiceId = invoice.InvoiceId,
            ScenarioCode = scenario,
            ExpectedOutcome = clean ? "MATCHED" : "EXCEPTION",
            ExpectedExceptionType = exceptionType,
            IsDuplicate = duplicate,
            HasPriceVariance = priceVariance,
            HasQuantityMismatch = quantityMismatch,
            HasMissingReceipt = missingReceipt,
            HasTaxOrVendorError = taxOrVendorError,
            IsEligibleForApproval = clean,
            IsEligibleForPayment = clean,
            Explanation = explanation
        };
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void ValidateInvoices(GeneratedDataset dataset)
    {
        if (dataset.Invoices.Count != InvoiceCount)
        {
            throw new InvalidOperationException(
                "Invoice count validation failed.");
        }

        if (dataset.InvoiceLines.Count != InvoiceCount)
        {
            throw new InvalidOperationException(
                "Invoice line count validation failed.");
        }

        if (dataset.GroundTruth.Count != InvoiceCount)
        {
            throw new InvalidOperationException(
                "Ground-truth count validation failed.");
        }

        var invoiceIds = dataset.Invoices
            .Select(x => x.InvoiceId)
            .ToHashSet();

        var vendorIds = dataset.Vendors
            .Select(x => x.VendorId)
            .ToHashSet();

        var poIds = dataset.PurchaseOrders
            .Select(x => x.PurchaseOrderId)
            .ToHashSet();

        foreach (var invoice in dataset.Invoices)
        {
            if (!vendorIds.Contains(invoice.VendorId))
            {
                throw new InvalidOperationException(
                    $"Invalid vendor reference in {invoice.InvoiceId}.");
            }

            if (invoice.PurchaseOrderId == null ||
                !poIds.Contains(invoice.PurchaseOrderId))
            {
                throw new InvalidOperationException(
                    $"Invalid PO reference in {invoice.InvoiceId}.");
            }

            var lines = dataset.InvoiceLines
                .Where(x => x.InvoiceId == invoice.InvoiceId)
                .ToList();

            if (lines.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Invoice has no lines: {invoice.InvoiceId}.");
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
                throw new InvalidOperationException(
                    $"Invoice arithmetic validation failed: {invoice.InvoiceId}.");
            }
        }

        foreach (var line in dataset.InvoiceLines)
        {
            if (!invoiceIds.Contains(line.InvoiceId))
            {
                throw new InvalidOperationException(
                    $"Invalid invoice reference in {line.InvoiceLineId}.");
            }

            if (line.Quantity <= 0 ||
                line.UnitPrice < 0 ||
                line.TaxRate < 0)
            {
                throw new InvalidOperationException(
                    $"Invalid invoice line values: {line.InvoiceLineId}.");
            }
        }

        var actualCounts = dataset.GroundTruth
            .GroupBy(x => x.ScenarioCode)
            .ToDictionary(x => x.Key, x => x.Count());

        foreach (var expected in ScenarioCounts)
        {
            if (!actualCounts.TryGetValue(
                    expected.Key,
                    out int actual) ||
                actual != expected.Value)
            {
                throw new InvalidOperationException(
                    $"Scenario count mismatch for {expected.Key}.");
            }
        }

        int duplicateNumberGroups = dataset.Invoices
            .GroupBy(x => new { x.VendorId, x.InvoiceNumber })
            .Count(x => x.Count() > 1);

        if (duplicateNumberGroups != ScenarioCounts["DUPLICATE_INVOICE"])
        {
            throw new InvalidOperationException(
                "Duplicate invoice validation failed.");
        }

        // Each ground-truth record must refer to exactly one invoice.
        if (dataset.GroundTruth
            .Select(x => x.InvoiceId)
            .Distinct()
            .Count() != InvoiceCount)
        {
            throw new InvalidOperationException(
                "Ground-truth invoice uniqueness validation failed.");
        }
    }
}
