using System.IO;
using System.Xml.Linq;
using static StatsClient.MVVM.Core.Functions;
namespace StatsClient.MVVM.Core.OrderCreation;

public sealed class OrderCreationGenerationService
{
    public const string DefaultCrownMaterial = "Zahn Zirconia";

    private readonly OrderCreationOrderXmlBuilder _orderXmlBuilder = new();
    private readonly OrderCreationMaterialsXmlBuilder _materialsXmlBuilder = new();

    public OrderCreationGenerationResult Generate(OrderCreationInput input)
    {
        string? createdOrderFolder = null;

        try
        {
            ValidateRequiredFields(input);
            OrderCreationOrderIdService.EnsureIntOrderIdIsAvailable(input.OrderID);
            NormalizeRestorations(input);
            ValidateRestorations(input);

            string globalMaterialsXmlPath = OrderCreationPaths.ResolveGlobalMaterialsXmlPath();
            string outputRoot = OrderCreationPaths.GetTempOutputRoot();
            string orderFolder = Path.Combine(outputRoot, input.OrderID);

            if (Directory.Exists(orderFolder))
            {
                if (Directory.EnumerateFileSystemEntries(orderFolder).Any())
                {
                    return new OrderCreationGenerationResult
                    {
                        Success = false,
                        Message = $"Temporary order folder already exists:\n{orderFolder}",
                    };
                }

                Directory.Delete(orderFolder);
            }

            OrderCreationConfigResolver resolver = new(globalMaterialsXmlPath);
            string uniqueNumOrderId = OrderCreationOrderIdService.AllocateUniqueNumOrderId();
            XDocument templateOrder = OrderCreationEmbeddedAssets.LoadTemplateOrderXml();

            XDocument orderDoc = _orderXmlBuilder.Build(input, templateOrder, resolver, uniqueNumOrderId);
            XDocument materialsDoc = _materialsXmlBuilder.Build(input, globalMaterialsXmlPath);

            Directory.CreateDirectory(orderFolder);
            createdOrderFolder = orderFolder;

            string orderXmlPath = Path.Combine(orderFolder, $"{input.OrderID}.xml");
            string materialsXmlPath = Path.Combine(orderFolder, "Materials.xml");
            string manufacturers3mlPath = OrderCreationEmbeddedAssets.CopyManufacturersFile(orderFolder);

            orderDoc.Save(orderXmlPath);
            materialsDoc.Save(materialsXmlPath);

            return new OrderCreationGenerationResult
            {
                Success = true,
                Message = "Order files created in temporary folder.",
                OrderFolderPath = orderFolder,
                OrderXmlPath = orderXmlPath,
                MaterialsXmlPath = materialsXmlPath,
                Manufacturers3mlPath = manufacturers3mlPath,
            };
        }
        catch (Exception ex)
        {
            TryDeleteEmptyDirectory(createdOrderFolder);

            return new OrderCreationGenerationResult
            {
                Success = false,
                Message = ex.Message,
            };
        }
    }

    public static OrderCreationInput CreateInput(
        string finalOrderId,
        string customer,
        string patientFirstName,
        string patientLastName,
        string teethNumbers,
        string? shade,
        string? comments,
        string? scannerOrderId,
        bool isScrewRetained)
    {
        string caseType = isScrewRetained ? "abutment" : "crown";
        string? normalizedShade = string.IsNullOrWhiteSpace(shade) ? null : shade.Trim();

        return new OrderCreationInput
        {
            OrderID = finalOrderId.Trim(),
            Customer = customer.Trim(),
            PatientFirstName = patientFirstName.Trim(),
            PatientLastName = patientLastName.Trim(),
            OperatorName = null,
            UnitNumbersCsv = teethNumbers,
            OrderComments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(),
            ExtOrderID = string.IsNullOrWhiteSpace(scannerOrderId) ? null : scannerOrderId.Trim(),
            ObjectType = "meIndicationRegular",
            ModelSetupEnabled = false,
            ManufacturerName = null,
            TeethSetup =
            [
                new OrderCreationRestorationInput
                {
                    CaseType = caseType,
                    Material = DefaultCrownMaterial,
                    Shade = normalizedShade,
                    Teeth = [],
                }
            ],
        };
    }

    private static void ValidateRequiredFields(OrderCreationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.OrderID))
        {
            throw new InvalidOperationException("Final order ID is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Customer))
        {
            throw new InvalidOperationException("Customer is required.");
        }

        if (string.IsNullOrWhiteSpace(input.PatientFirstName) || string.IsNullOrWhiteSpace(input.PatientLastName))
        {
            throw new InvalidOperationException("Patient first and last name are required.");
        }

        if (string.IsNullOrWhiteSpace(input.UnitNumbersCsv)
            && (input.UnitNumbers is null || input.UnitNumbers.Count == 0)
            && input.TeethSetup.All(x => x.Teeth.Count == 0))
        {
            throw new InvalidOperationException("At least one tooth number is required.");
        }
    }

    private static void ValidateRestorations(OrderCreationInput input)
    {
        if (input.TeethSetup.Count == 0)
        {
            throw new InvalidOperationException("At least one tooth number is required.");
        }

        foreach (OrderCreationRestorationInput setup in input.TeethSetup)
        {
            if (setup.Teeth.Count == 0)
            {
                throw new InvalidOperationException("Each restoration must define at least one tooth.");
            }

            if (string.IsNullOrWhiteSpace(setup.Material))
            {
                throw new InvalidOperationException("Each restoration must define a material.");
            }
        }
    }

    private static void NormalizeRestorations(OrderCreationInput input)
    {
        List<int> unitNumbers = GetRequestedUnitNumbers(input)
            .Where(x => x is >= 1 and <= 32)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (unitNumbers.Count == 0)
        {
            throw new InvalidOperationException("At least one valid tooth number (1-32) is required.");
        }

        string caseType = input.TeethSetup.FirstOrDefault()?.CaseType;
        if (string.IsNullOrWhiteSpace(caseType))
        {
            caseType = "crown";
        }

        string material = input.TeethSetup.FirstOrDefault()?.Material;
        if (string.IsNullOrWhiteSpace(material))
        {
            material = DefaultCrownMaterial;
        }

        string? shade = input.TeethSetup.FirstOrDefault()?.Shade;
        if (string.IsNullOrWhiteSpace(shade))
        {
            shade = null;
        }

        input.ObjectType = "meIndicationRegular";
        input.ModelSetupEnabled = false;
        input.ManufacturerName = null;

        input.TeethSetup = unitNumbers
            .Select(unit => new OrderCreationRestorationInput
            {
                CaseType = caseType,
                Teeth = [unit],
                Material = material,
                Shade = shade,
                Pontics = null,
                BridgeStyle = null,
            })
            .ToList();
    }

    private static IEnumerable<int> GetRequestedUnitNumbers(OrderCreationInput input)
    {
        if (input.UnitNumbers is { Count: > 0 })
        {
            return input.UnitNumbers;
        }

        if (!string.IsNullOrWhiteSpace(input.UnitNumbersCsv))
        {
            return input.UnitNumbersCsv
                .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out int parsed) ? parsed : -1)
                .Where(x => x > 0);
        }

        if (input.TeethSetup.Count > 0)
        {
            return input.TeethSetup.SelectMany(x => x.Teeth);
        }

        return [];
    }

    private static void TryDeleteEmptyDirectory(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath);
            }
        }
        catch
        {
        }
    }
}
