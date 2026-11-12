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