
namespace InvoiceIQ.DataGenerator.Models;

// ============================================================
// VENDOR
// ============================================================

public class Vendor
{
    public string VendorId { get; set; } = string.Empty;
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Currency { get; set; } = "INR";
    public string Country { get; set; } = "India";
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

// ============================================================
// PURCHASE ORDER
// ============================================================

public class PurchaseOrder
{
    public string PurchaseOrderId { get; set; } = string.Empty;
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public string VendorId { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string Currency { get; set; } = "INR";
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "OPEN";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

// ============================================================
// PURCHASE ORDER LINE
// ============================================================

public class PurchaseOrderLine
{
    public string PurchaseOrderLineId { get; set; } = string.Empty;
    public string PurchaseOrderId { get; set; } = string.Empty;
    public string LineReference { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public string TaxCode { get; set; } = string.Empty;
    public decimal LineAmount { get; set; }
}

// ============================================================
// GOODS RECEIPT
// ============================================================

public class GoodsReceipt
{
    public string GoodsReceiptId { get; set; } = string.Empty;
    public string GoodsReceiptNumber { get; set; } = string.Empty;
    public string PurchaseOrderId { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public string Status { get; set; } = "RECEIVED";
}

// ============================================================
// GOODS RECEIPT LINE
// ============================================================

public class GoodsReceiptLine
{
    public string GoodsReceiptLineId { get; set; } = string.Empty;
    public string GoodsReceiptId { get; set; } = string.Empty;
    public string PurchaseOrderLineId { get; set; } = string.Empty;
    public decimal ReceivedQuantity { get; set; }
}

// ============================================================
// INVOICE
// ============================================================

public class Invoice
{
    public string InvoiceId { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string VendorId { get; set; } = string.Empty;
    public string? PurchaseOrderId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Currency { get; set; } = "INR";
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = "RECEIVED";
    public DateTime CreatedAt { get; set; }

    // Data-generation metadata; never use as model input.
  
}

// ============================================================
// INVOICE LINE
// ============================================================

public class InvoiceLine
{
    public string InvoiceLineId { get; set; } = string.Empty;
    public string InvoiceId { get; set; } = string.Empty;
    public string LineReference { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public string TaxCode { get; set; } = string.Empty;
    public decimal LineAmount { get; set; }
}

// ============================================================
// GROUND TRUTH
// ============================================================

public class GroundTruth
{
    public string InvoiceId { get; set; } = string.Empty;
    public string ScenarioCode { get; set; } = string.Empty;
    public string ExpectedOutcome { get; set; } = string.Empty;
    public string ExpectedExceptionType { get; set; } = string.Empty;
    public bool IsDuplicate { get; set; }
    public bool HasPriceVariance { get; set; }
    public bool HasQuantityMismatch { get; set; }
    public bool HasMissingReceipt { get; set; }
    public bool HasTaxOrVendorError { get; set; }
    public bool IsEligibleForApproval { get; set; }
    public bool IsEligibleForPayment { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

// ============================================================
// COMPLETE GENERATED DATASET
// ============================================================

public class GeneratedDataset
{
    public List<Vendor> Vendors { get; set; } = [];
    public List<PurchaseOrder> PurchaseOrders { get; set; } = [];
    public List<PurchaseOrderLine> PurchaseOrderLines { get; set; } = [];
    public List<GoodsReceipt> GoodsReceipts { get; set; } = [];
    public List<GoodsReceiptLine> GoodsReceiptLines { get; set; } = [];
    public List<Invoice> Invoices { get; set; } = [];
    public List<InvoiceLine> InvoiceLines { get; set; } = [];
    public List<GroundTruth> GroundTruth { get; set; } = [];
}
