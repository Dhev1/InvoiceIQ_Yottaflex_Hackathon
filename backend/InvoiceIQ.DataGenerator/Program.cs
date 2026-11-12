
using InvoiceIQ.DataGenerator.Exports;
using InvoiceIQ.DataGenerator.Services;

try
{
    int scale = 1;
    string? outputArgument = null;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--scale":
                if (i + 1 >= args.Length ||
                    !int.TryParse(args[++i], out scale))
                {
                    throw new ArgumentException(
                        "Provide a valid integer after --scale.");
                }
                break;

            case "--output":
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException(
                        "Provide an output directory after --output.");
                }

                outputArgument = args[++i];
                break;

            case "--help":
                Console.WriteLine("""
                    InvoiceIQ Data Generator

                    Usage:
                      dotnet run -- --scale 1
                      dotnet run -- --scale 2
                      dotnet run -- --scale 1 --output C:\InvoiceIQData

                    Options:
                      --scale    Dataset multiplier (1 to 100).
                      --output   Custom output directory.
                      --help     Display this help.
                    """);

                return 0;

            default:
                throw new ArgumentException(
                    $"Unknown argument: {args[i]}");
        }
    }

    if (scale < 1 || scale > 100)
    {
        throw new ArgumentOutOfRangeException(
            nameof(scale),
            "Scale must be between 1 and 100.");
    }

    Console.WriteLine("======================================");
    Console.WriteLine("       InvoiceIQ Data Generator       ");
    Console.WriteLine("======================================");
    Console.WriteLine($"Seed: 20261022");
    Console.WriteLine($"Scale: {scale}");
    Console.WriteLine();

    Console.WriteLine("[1/3] Generating vendors, POs and receipts...");

    var dataset = new MasterDataGenerator(scale).Generate();

    Console.WriteLine("[2/3] Generating invoices and ground truth...");

    new InvoiceGenerator(scale).Generate(dataset);

    Console.WriteLine("[3/3] Exporting dataset...");

    string outputDirectory = outputArgument ??
        Path.Combine(
            Directory.GetCurrentDirectory(),
            "Output",
            $"seed-20261022-scale-{scale}");

    outputDirectory = Path.GetFullPath(outputDirectory);

    new DatasetExporter().Export(
        dataset,
        outputDirectory);

    Console.WriteLine();
    Console.WriteLine("========== GENERATED RECORDS ==========");
    Console.WriteLine($"Vendors:              {dataset.Vendors.Count:N0}");
    Console.WriteLine($"Purchase orders:      {dataset.PurchaseOrders.Count:N0}");
    Console.WriteLine($"PO lines:             {dataset.PurchaseOrderLines.Count:N0}");
    Console.WriteLine($"Goods receipts:       {dataset.GoodsReceipts.Count:N0}");
    Console.WriteLine($"Goods receipt lines:  {dataset.GoodsReceiptLines.Count:N0}");
    Console.WriteLine($"Invoices:             {dataset.Invoices.Count:N0}");
    Console.WriteLine($"Invoice lines:        {dataset.InvoiceLines.Count:N0}");
    Console.WriteLine($"Ground truth:         {dataset.GroundTruth.Count:N0}");
    Console.WriteLine("======================================");

    Console.WriteLine();
    Console.WriteLine("Generation completed.");
    Console.WriteLine($"Output: {outputDirectory}");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("DATA GENERATION FAILED");
    Console.Error.WriteLine(ex.Message);

    return 1;
}
