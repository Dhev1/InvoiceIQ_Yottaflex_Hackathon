
using InvoiceIQ.DataGenerator.Models;

namespace InvoiceIQ.DataGenerator.Services;

public class MasterDataGenerator
{
    private const int Seed = 20261022;

    private readonly int _scale;
    private readonly Random _random = new(Seed);

    private readonly DateTime _startDate = new(2025, 1, 1);
    private readonly DateTime _endDate = new(2026, 10, 31);

    private int VendorCount => 300 * _scale;
    private int PurchaseOrderCount => 1500 * _scale;
    private int PurchaseOrderLineCount => 2200 * _scale;
    private int GoodsReceiptCount => 1800 * _scale;

    private readonly string[] _vendorPrefixes =
    [
        "Apex", "Global", "Prime", "Vertex", "Metro",
        "Dynamic", "Elite", "National", "Universal", "NextGen",
        "Reliable", "Summit", "Pioneer", "Alpha", "Omega"
    ];

    private readonly string[] _vendorIndustries =
    [
        "Electronics", "Industrial Supplies", "Office Solutions",
        "Components", "Technologies", "Manufacturing",
        "Distributors", "Engineering", "Systems", "Enterprises"
    ];

    private readonly string[] _productCodes =
    [
        "ELEC-001", "ELEC-002", "ELEC-003", "ELEC-004",
        "OFF-001", "OFF-002", "OFF-003",
        "IND-001", "IND-002", "IND-003",
        "COMP-001", "COMP-002", "COMP-003",
        "IT-001", "IT-002", "IT-003",
        "PACK-001", "PACK-002", "PACK-003",
        "MISC-001"
    ];

    private readonly Dictionary<string, string> _productDescriptions =
        new()
        {
            ["ELEC-001"] = "Electrical Control Unit",
            ["ELEC-002"] = "Industrial Power Cable",
            ["ELEC-003"] = "Digital Sensor Module",
            ["ELEC-004"] = "Power Supply Assembly",
            ["OFF-001"] = "Office Paper Ream",
            ["OFF-002"] = "Printer Cartridge",
            ["OFF-003"] = "Office Furniture",
            ["IND-001"] = "Industrial Motor",
            ["IND-002"] = "Steel Fastener Kit",
            ["IND-003"] = "Industrial Valve",
            ["COMP-001"] = "Electronic Circuit Board",
            ["COMP-002"] = "Connector Assembly",
            ["COMP-003"] = "Microcontroller Module",
            ["IT-001"] = "Network Switch",
            ["IT-002"] = "Computer Peripheral",
            ["IT-003"] = "Storage Device",
            ["PACK-001"] = "Packaging Carton",
            ["PACK-002"] = "Protective Packaging",
            ["PACK-003"] = "Shipping Material",
            ["MISC-001"] = "General Maintenance Material"
        };

    private readonly (string Code, decimal Rate)[] _taxCategories =
    [
        ("GST5", 0.05m),
        ("GST12", 0.12m),
        ("GST18", 0.18m),
        ("GST28", 0.28m)
    ];

    public MasterDataGenerator(int scale = 1)
    {
        if (scale < 1 || scale > 100)
            throw new ArgumentOutOfRangeException(
                nameof(scale),
                "Scale must be between 1 and 100.");

        _scale = scale;
    }

    public GeneratedDataset Generate()
    {
        var dataset = new GeneratedDataset();

        GenerateVendors(dataset);
        GeneratePurchaseOrders(dataset);
        GenerateGoodsReceipts(dataset);

        ValidateMasterData(dataset);

        return dataset;
    }

    private void GenerateVendors(GeneratedDataset dataset)
    {
        for (int i = 1; i <= VendorCount; i++)
        {
            string vendorCode = $"VND-{i:D5}";

            string prefix = Pick(_vendorPrefixes);
            string industry = Pick(_vendorIndustries);

            dataset.Vendors.Add(new Vendor
            {
                VendorId = vendorCode,
                VendorCode = vendorCode,
                VendorName = $"{prefix} {industry} {i:D5}",
                Email = $"accounts{i:D5}@vendor.example",
                Currency = "INR",
                Country = "India",
                IsActive = true,
                EffectiveFrom = new DateTime(2024, 1, 1),
                EffectiveTo = null
            });
        }
    }

    private void GeneratePurchaseOrders(GeneratedDataset dataset)
    {
        int lineCounter = 0;

        for (int i = 1; i <= PurchaseOrderCount; i++)
        {
            string poNumber = $"PO-2026-{i:D6}";

            var vendor = dataset.Vendors[
                _random.Next(dataset.Vendors.Count)];

            DateTime orderDate = RandomDate(
                _startDate,
                _endDate.AddDays(-30));

            var purchaseOrder = new PurchaseOrder
            {
                PurchaseOrderId = poNumber,
                PurchaseOrderNumber = poNumber,
                VendorId = vendor.VendorId,
                OrderDate = orderDate,
                DeliveryDate = orderDate.AddDays(
                    _random.Next(5, 31)),
                Currency = vendor.Currency,
                TotalAmount = 0m,
                Status = "OPEN",
                EffectiveFrom = orderDate,
                EffectiveTo = null
            };

            // 800 single-line and 700 two-line POs per scale.
            int numberOfLines = i <= 800 * _scale ? 1 : 2;

            var selectedProducts = new HashSet<string>();

            for (int lineIndex = 1;
                 lineIndex <= numberOfLines;
                 lineIndex++)
            {
                lineCounter++;

                string productCode;

                do
                {
                    productCode = Pick(_productCodes);
                }
                while (!selectedProducts.Add(productCode));

                var tax = Pick(_taxCategories);

                decimal quantity = RandomQuantity();
                decimal unitPrice = RandomUnitPrice();

                decimal baseAmount = RoundMoney(
                    quantity * unitPrice);

                decimal taxAmount = RoundMoney(
                    baseAmount * tax.Rate);

                decimal lineAmount = baseAmount + taxAmount;

                dataset.PurchaseOrderLines.Add(
                    new PurchaseOrderLine
                    {
                        PurchaseOrderLineId =
                            $"POL-{lineCounter:D7}",

                        PurchaseOrderId =
                            purchaseOrder.PurchaseOrderId,

                        LineReference = lineIndex.ToString("D2"),
                        ProductCode = productCode,
                        Description = _productDescriptions[productCode],
                        OrderedQuantity = quantity,
                        UnitPrice = unitPrice,
                        TaxRate = tax.Rate,
                        TaxCode = tax.Code,
                        LineAmount = lineAmount
                    });

                purchaseOrder.TotalAmount += lineAmount;
            }

            purchaseOrder.TotalAmount =
                RoundMoney(purchaseOrder.TotalAmount);

            dataset.PurchaseOrders.Add(purchaseOrder);
        }

        if (lineCounter != PurchaseOrderLineCount)
        {
            throw new InvalidOperationException(
                $"Expected {PurchaseOrderLineCount} PO lines, generated {lineCounter}.");
        }
    }

    private void GenerateGoodsReceipts(GeneratedDataset dataset)
    {
        int receiptCounter = 0;
        int receiptLineCounter = 0;

        foreach (var purchaseOrder in dataset.PurchaseOrders)
        {
            int poIndex = int.Parse(
                purchaseOrder.PurchaseOrderNumber[^6..]);

            // Per scale:
            // 300 POs: no receipt
            // 600 POs: one receipt
            // 600 POs: two receipts
            // Total: 1,800 receipts.

            int receiptCount =
                poIndex <= 300 * _scale ? 0 :
                poIndex <= 900 * _scale ? 1 :
                2;

            var poLines = dataset.PurchaseOrderLines
                .Where(x =>
                    x.PurchaseOrderId ==
                    purchaseOrder.PurchaseOrderId)
                .ToList();

            for (int receiptIndex = 1;
                 receiptIndex <= receiptCount;
                 receiptIndex++)
            {
                receiptCounter++;

                DateTime receiptDate =
                    purchaseOrder.OrderDate.AddDays(
                        receiptCount == 2
                            ? receiptIndex * 5
                            : _random.Next(5, 21));

                var receipt = new GoodsReceipt
                {
                    GoodsReceiptId =
                        $"GRN-2026-{receiptCounter:D6}",

                    GoodsReceiptNumber =
                        $"GRN-2026-{receiptCounter:D6}",

                    PurchaseOrderId =
                        purchaseOrder.PurchaseOrderId,

                    ReceiptDate = receiptDate,
                    Status = "RECEIVED"
                };

                dataset.GoodsReceipts.Add(receipt);

                foreach (var poLine in poLines)
                {
                    decimal receivedQuantity =
                        receiptCount == 2
                            ? receiptIndex == 1
                                ? poLine.OrderedQuantity / 2m
                                : poLine.OrderedQuantity -
                                  poLine.OrderedQuantity / 2m
                            : poLine.OrderedQuantity;

                    receiptLineCounter++;

                    dataset.GoodsReceiptLines.Add(
                        new GoodsReceiptLine
                        {
                            GoodsReceiptLineId =
                                $"GRNL-{receiptLineCounter:D7}",

                            GoodsReceiptId =
                                receipt.GoodsReceiptId,

                            PurchaseOrderLineId =
                                poLine.PurchaseOrderLineId,

                            ReceivedQuantity = receivedQuantity
                        });
                }
            }

            purchaseOrder.Status =
                receiptCount == 0 ? "OPEN" : "RECEIVED";
        }

        if (receiptCounter != GoodsReceiptCount)
        {
            throw new InvalidOperationException(
                $"Expected {GoodsReceiptCount} goods receipts, generated {receiptCounter}.");
        }
    }

    private void ValidateMasterData(GeneratedDataset dataset)
    {
        if (dataset.Vendors.Count != 300 * _scale)
            throw new InvalidOperationException("Vendor count validation failed.");

        if (dataset.PurchaseOrders.Count != 1500 * _scale)
            throw new InvalidOperationException("PO count validation failed.");

        if (dataset.PurchaseOrderLines.Count != 2200 * _scale)
            throw new InvalidOperationException("PO line count validation failed.");

        if (dataset.GoodsReceipts.Count != 1800 * _scale)
            throw new InvalidOperationException("Goods receipt count validation failed.");

        var vendorIds = dataset.Vendors
            .Select(x => x.VendorId)
            .ToHashSet();

        var poIds = dataset.PurchaseOrders
            .Select(x => x.PurchaseOrderId)
            .ToHashSet();

        var poLineIds = dataset.PurchaseOrderLines
            .Select(x => x.PurchaseOrderLineId)
            .ToHashSet();

        foreach (var po in dataset.PurchaseOrders)
        {
            if (!vendorIds.Contains(po.VendorId))
                throw new InvalidOperationException(
                    $"Invalid vendor reference in {po.PurchaseOrderNumber}.");
        }

        foreach (var line in dataset.PurchaseOrderLines)
        {
            if (!poIds.Contains(line.PurchaseOrderId))
                throw new InvalidOperationException(
                    $"Invalid PO reference in {line.PurchaseOrderLineId}.");

            if (line.OrderedQuantity <= 0 ||
                line.UnitPrice < 0 ||
                line.LineAmount < 0)
            {
                throw new InvalidOperationException(
                    $"Invalid PO line values: {line.PurchaseOrderLineId}.");
            }
        }

        foreach (var receipt in dataset.GoodsReceipts)
        {
            if (!poIds.Contains(receipt.PurchaseOrderId))
                throw new InvalidOperationException(
                    $"Invalid PO reference in {receipt.GoodsReceiptNumber}.");
        }

        foreach (var line in dataset.GoodsReceiptLines)
        {
            if (!poLineIds.Contains(line.PurchaseOrderLineId))
                throw new InvalidOperationException(
                    $"Invalid PO line reference in {line.GoodsReceiptLineId}.");
        }

        foreach (var po in dataset.PurchaseOrders)
        {
            decimal calculatedTotal = dataset.PurchaseOrderLines
                .Where(x => x.PurchaseOrderId == po.PurchaseOrderId)
                .Sum(x => x.LineAmount);

            if (po.TotalAmount != calculatedTotal)
                throw new InvalidOperationException(
                    $"PO total mismatch: {po.PurchaseOrderNumber}.");
        }
    }

    private string Pick(string[] values) =>
        values[_random.Next(values.Length)];

    private (string Code, decimal Rate) Pick(
        (string Code, decimal Rate)[] values) =>
        values[_random.Next(values.Length)];

    private DateTime RandomDate(DateTime minimum, DateTime maximum)
    {
        int days = (maximum - minimum).Days;
        return minimum.AddDays(_random.Next(days + 1));
    }

    private decimal RandomQuantity() =>
        _random.Next(2, 101) / 2m;

    private decimal RandomUnitPrice() =>
        Math.Round(
            _random.Next(100, 100001) / 100m,
            2,
            MidpointRounding.AwayFromZero);

    private static decimal RoundMoney(decimal amount) =>
        Math.Round(
            amount,
            2,
            MidpointRounding.AwayFromZero);
}
