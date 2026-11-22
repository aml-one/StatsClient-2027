using System.Xml.Linq;

namespace StatsClient.MVVM.Core.OrderCreation;

internal sealed class OrderCreationOrderXmlBuilder
{
    public XDocument Build(
        OrderCreationInput input,
        XDocument templateOrderXml,
        OrderCreationConfigResolver resolver,
        string uniqueNumOrderId)
    {
        XDocument doc = new(templateOrderXml);

        XElement orderItem = doc.Descendants("Object")
            .First(x => (string?)x.Attribute("type") == "TDM_Item_Order");

        SetProperty(orderItem, "IntOrderID", input.OrderID);
        SetProperty(orderItem, "NumOrderID", uniqueNumOrderId);
        SetProperty(orderItem, "Patient_FirstName", input.PatientFirstName);
        SetProperty(orderItem, "Patient_LastName", input.PatientLastName);
        SetProperty(orderItem, "Customer", input.Customer);
        SetProperty(orderItem, "ManufName", ResolveOrderManufacturerName(input));
        SetProperty(orderItem, "OperatorName", string.Empty);
        SetProperty(orderItem, "ScanModuleID", "ModelBuilder");

        if (!string.IsNullOrWhiteSpace(input.ExtOrderID))
        {
            SetProperty(orderItem, "ExtOrderID", input.ExtOrderID);
        }

        if (!string.IsNullOrWhiteSpace(input.OrderComments))
        {
            SetProperty(orderItem, "OrderComments", input.OrderComments);
        }

        string itemsSummary = BuildItemsSummary(input);
        SetProperty(orderItem, "Items", itemsSummary);

        string cacheMaterials = string.Join(",", input.TeethSetup.Select(x => x.Material).Distinct(StringComparer.OrdinalIgnoreCase));
        if (input.ModelSetupEnabled && !cacheMaterials.Contains("Model Material", StringComparison.OrdinalIgnoreCase))
        {
            cacheMaterials = string.IsNullOrEmpty(cacheMaterials) ? "Model Material" : $"{cacheMaterials},\"Model Material\"";
        }

        SetProperty(orderItem, "CacheMaterialName", cacheMaterials);

        XElement modelJobList = GetItemsList(doc, "ModelJobList");
        XElement modelElementList = GetItemsList(doc, "ModelElementList");
        XElement toothElementList = GetItemsList(doc, "ToothElementList");
        XElement linkList = GetItemsList(doc, "LinkList");
        XElement linkToothElementList = GetItemsList(doc, "LinkToothElementList");
        XElement scanList = GetItemsList(doc, "ScanList");
        XElement customDataList = GetItemsList(doc, "CustomDataList");

        XElement? templateModelElement = modelElementList.Elements("Object").FirstOrDefault();
        string templateModelManufName = (templateModelElement is not null
            ? GetTemplatePropertyValue(templateModelElement, "ManufName")
            : null)
            ?? GetTemplatePropertyValue(orderItem, "ManufName")
            ?? ResolveOrderManufacturerName(input);

        modelJobList.RemoveNodes();
        modelElementList.RemoveNodes();
        toothElementList.RemoveNodes();
        linkList.RemoveNodes();
        linkToothElementList.RemoveNodes();
        scanList.RemoveNodes();

        UpdateCustomDataList(customDataList, input);

        string indicationJobId = OrderCreationIdFactory.NewId("MJ");
        modelJobList.Add(NewModelJob(indicationJobId, input.OrderID));

        string? modelJobId = null;
        if (input.ModelSetupEnabled)
        {
            modelJobId = OrderCreationIdFactory.NewId("MJ");
            modelJobList.Add(NewModelJob(modelJobId, input.OrderID));
        }

        foreach (OrderCreationRestorationInput setup in input.TeethSetup)
        {
            bool isBridge = setup.CaseType.Equals("bridge", StringComparison.OrdinalIgnoreCase);
            string modelElementId = OrderCreationIdFactory.NewId("ME");
            string normalizedMaterial = resolver.NormalizeMaterialForCase(setup.CaseType, setup.Material);

            OrderCreationMaterialConfig materialConfig = resolver.ResolveMaterial(normalizedMaterial);
            string materialId = OrderCreationConfigResolver.GetMaterialId(materialConfig);
            string processId = OrderCreationConfigResolver.ResolveManufacturingProcessId(setup.CaseType, normalizedMaterial);
            string colorId = OrderCreationConfigResolver.ResolveColorId(setup.Shade);
            string itemText = BuildItemText(setup);
            string cacheMaterialName = setup.CaseType.Equals("abutment", StringComparison.OrdinalIgnoreCase)
                ? "Ti, 3Shape 4.3mm implant, 1 pcs Abutment 4.3 mm"
                : normalizedMaterial;

            modelElementList.Add(NewModelElement(
                modelElementId,
                indicationJobId,
                materialId,
                colorId,
                processId,
                input.ManufacturerName ?? templateModelManufName,
                itemText,
                cacheMaterialName,
                input.ObjectType));

            Dictionary<int, string> toothElementIds = [];
            foreach (int tooth in setup.Teeth)
            {
                bool isPontic = isBridge && setup.Pontics?.Contains(tooth) == true;
                string toothElementId = OrderCreationIdFactory.NewId("TE");
                toothElementIds[tooth] = toothElementId;

                string toothTypeId = OrderCreationConfigResolver.ResolveToothElementTypeId(setup.CaseType, normalizedMaterial, isPontic);
                string toothClass = OrderCreationConfigResolver.ResolveCacheToothTypeClass(setup.CaseType, isPontic);
                string abutmentKitId = setup.CaseType.Equals("abutment", StringComparison.OrdinalIgnoreCase)
                    ? "3S_AbutmentKit_1pc_4_3"
                    : string.Empty;

                toothElementList.Add(NewToothElement(toothElementId, modelElementId, toothTypeId, tooth, abutmentKitId, toothClass));
            }

            if (isBridge && setup.Teeth.Count > 1)
            {
                for (int i = 0; i < setup.Teeth.Count - 1; i++)
                {
                    int left = setup.Teeth[i];
                    int right = setup.Teeth[i + 1];
                    string linkId = OrderCreationIdFactory.NewId("LF");
                    linkList.Add(NewLink(linkId));
                    linkToothElementList.Add(NewLinkToothElement(OrderCreationIdFactory.NewId("LT"), toothElementIds[left], linkId));
                    linkToothElementList.Add(NewLinkToothElement(OrderCreationIdFactory.NewId("LT"), toothElementIds[right], linkId));
                }
            }
        }

        if (input.ModelSetupEnabled && modelJobId is not null)
        {
            OrderCreationMaterialConfig modelMaterial = resolver.ResolveMaterial("Model Material");
            string modelElementId = OrderCreationIdFactory.NewId("ME");
            modelElementList.Add(NewModelElement(
                modelElementId,
                modelJobId,
                OrderCreationConfigResolver.GetMaterialId(modelMaterial),
                "_NULL_",
                "Configuration_11_ModelManufacturing",
                input.ManufacturerName ?? templateModelManufName,
                "Unsectioned model",
                "Model Material",
                "meDigitalModelPrepUnsectioned"));
        }

        scanList.Add(NewScan(OrderCreationIdFactory.NewId("S"), "stPreparationIntraOral", indicationJobId));

        if (input.TeethSetup.Any(x => x.CaseType.Equals("abutment", StringComparison.OrdinalIgnoreCase)))
        {
            scanList.Add(NewScan(OrderCreationIdFactory.NewId("S"), "stAbutmentAlignment", modelJobId ?? indicationJobId));
        }

        scanList.Add(NewScan(OrderCreationIdFactory.NewId("S"), "stAntagonistIntraOral", modelJobId ?? indicationJobId));

        return doc;
    }

    private static string BuildItemsSummary(OrderCreationInput input)
    {
        List<string> parts = [];
        foreach (OrderCreationRestorationInput r in input.TeethSetup)
        {
            parts.Add(BuildItemText(r));
        }

        if (input.ModelSetupEnabled)
        {
            parts.Add("Unsectioned model");
        }

        return string.Join(", ", parts);
    }

    private static string BuildItemText(OrderCreationRestorationInput r)
    {
        return r.CaseType.ToLowerInvariant() switch
        {
            "bridge" => $"Anatomy bridge {r.Teeth.Min()}-{r.Teeth.Max()}",
            "veneer" => $"Veneer {string.Join("/", r.Teeth)}",
            "abutment" => $"Abutment {string.Join("/", r.Teeth)}",
            "crown" => $"Crown {string.Join("/", r.Teeth)}",
            "crownonabutment" => $"Crown {string.Join("/", r.Teeth)}",
            _ => $"{r.CaseType} {string.Join("/", r.Teeth)}",
        };
    }

    private static void UpdateCustomDataList(XElement customDataList, OrderCreationInput input)
    {
        string? shade = input.TeethSetup
            .Select(x => x.Shade)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        foreach (XElement customDataItem in customDataList.Elements("Object"))
        {
            SetProperty(customDataItem, "OrderID", input.OrderID);

            string? fieldId = GetTemplatePropertyValue(customDataItem, "FieldID");
            if (fieldId == "DS_ShadeField" && !string.IsNullOrWhiteSpace(shade))
            {
                SetProperty(customDataItem, "Value", shade);
            }
        }
    }

    private static string? GetTemplatePropertyValue(XElement objectNode, string propertyName)
    {
        return objectNode.Elements("Property")
            .FirstOrDefault(x => (string?)x.Attribute("name") == propertyName)
            ?.Attribute("value")
            ?.Value;
    }

    private static string ResolveOrderManufacturerName(OrderCreationInput input)
    {
        bool usesIvoclarMaterial = input.TeethSetup.Any(setup =>
            setup.Material.Contains("Ivoclar", StringComparison.OrdinalIgnoreCase));

        if (usesIvoclarMaterial && input.Customer.Equals("Denslab Gugler", StringComparison.OrdinalIgnoreCase))
        {
            return "3D Medical Print/Denslab Gugler";
        }

        return "3D Medical Print";
    }

    private static XElement GetItemsList(XDocument doc, string objectName)
    {
        return doc.Descendants("Object")
            .First(x => (string?)x.Attribute("name") == objectName)
            .Element("List")!;
    }

    private static XElement NewModelJob(string modelJobId, string orderId)
    {
        return new XElement("Object",
            new XAttribute("type", "TDM_Item_ModelJob"),
            NewProperty("ModelJobID", modelJobId),
            NewProperty("OrderID", orderId));
    }

    private static XElement NewModelElement(
        string modelElementId,
        string modelJobId,
        string materialId,
        string colorId,
        string manufacturingProcessId,
        string manufacturerName,
        string items,
        string cacheMaterialName,
        string modelElementType)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        long createDate = now.ToUnixTimeSeconds();
        long deliveryDate = createDate > 40 ? createDate - 40 : createDate;
        long ship = new DateTimeOffset(now.Date.AddDays(2), TimeSpan.Zero).ToUnixTimeSeconds();

        return new XElement("Object",
            new XAttribute("type", "TDM_Item_ModelElement"),
            NewProperty("ModelElementID", modelElementId),
            NewProperty("ModelJobID", modelJobId),
            NewProperty("MaterialID", materialId),
            NewProperty("ColorID", colorId),
            NewProperty("ProcessStatusID", "psCreated"),
            NewProperty("AltProcessStatusID", "psCreated"),
            NewProperty("ProcessLockID", "plReady"),
            NewProperty("ManufacturingProcessID", manufacturingProcessId),
            NewProperty("ManufacturerID", "30127"),
            NewProperty("ModelHeight", "0"),
            NewProperty("ModelVolume", "0"),
            NewProperty("ModelBoundingBoxMin", string.Empty),
            NewProperty("ModelBoundingBoxMax", string.Empty),
            NewProperty("ModelTransformation", string.Empty),
            NewProperty("ModelFilename", string.Empty),
            NewProperty("Location1", string.Empty),
            NewProperty("Location2", string.Empty),
            NewProperty("Location3", string.Empty),
            NewProperty("Location4", string.Empty),
            NewProperty("VirtualItem", "False"),
            NewProperty("ModelComment", string.Empty),
            NewProperty("CreateDate", createDate.ToString()),
            NewProperty("DeliveryDate", deliveryDate.ToString()),
            NewProperty("ShippingDate", ship.ToString()),
            NewProperty("ReceiveDate", ship.ToString()),
            NewProperty("ModelActive", "True"),
            NewProperty("WasSent", "False"),
            NewProperty("Items", items),
            NewProperty("ManufName", manufacturerName),
            NewProperty("CacheMaterialName", cacheMaterialName),
            NewProperty("CacheColor", string.Empty),
            NewProperty("CAMBlankID", string.Empty),
            NewProperty("CAMBlankBatchID", string.Empty),
            NewProperty("CAMJobID", string.Empty),
            NewProperty("CAMJobName", string.Empty),
            NewProperty("CAMErrorDescription", string.Empty),
            NewProperty("ERPItemNo", string.Empty),
            NewProperty("ModelElementType", modelElementType),
            NewProperty("ValidationResult", "vrUndefined"),
            NewProperty("IgnoredMaterialRestrictions", string.Empty));
    }

    private static XElement NewToothElement(
        string toothElementId,
        string modelElementId,
        string toothTypeId,
        int toothNumber,
        string abutmentKitId,
        string toothClass)
    {
        return new XElement("Object",
            new XAttribute("type", "TDM_Item_ToothElement"),
            NewProperty("ToothElementID", toothElementId),
            NewProperty("ModelElementID", modelElementId),
            NewProperty("toothElementTypeID", toothTypeId),
            NewProperty("ToothNumber", toothNumber.ToString()),
            NewProperty("AbutmentKitID", abutmentKitId),
            NewProperty("Anatomical", "False"),
            NewProperty("PostAndCore", "False"),
            NewProperty("CacheToothTypeClass", toothClass));
    }

    private static XElement NewLink(string linkId)
    {
        return new XElement("Object",
            new XAttribute("type", "TDM_Item_Link"),
            NewProperty("LinkID", linkId),
            NewProperty("LinkTypeID", "LinkTypeConnector6"));
    }

    private static XElement NewLinkToothElement(string linkToothElementId, string toothElementId, string linkId)
    {
        return new XElement("Object",
            new XAttribute("type", "TDM_Item_LinkToothElement"),
            NewProperty("LinkToothElementID", linkToothElementId),
            NewProperty("ToothElementID", toothElementId),
            NewProperty("LinkID", linkId));
    }

    private static XElement NewScan(string scanId, string scanType, string modelJobId)
    {
        return new XElement("Object",
            new XAttribute("type", "TDM_Item_Scan"),
            NewProperty("ScanID", scanId),
            NewProperty("ScanType", NormalizeScanType(scanType)),
            NewProperty("ModelJobID", modelJobId),
            NewProperty("ModelElementID", "_NULL_"),
            NewProperty("ToothElementID", "_NULL_"),
            NewProperty("ScanDate", "0"),
            NewProperty("ScanName", string.Empty),
            NewProperty("FileName", string.Empty));
    }

    private static string NormalizeScanType(string scanType)
    {
        if (scanType.EndsWith("IntraOral", StringComparison.OrdinalIgnoreCase))
        {
            return scanType;
        }

        return scanType switch
        {
            "stPreperation" => "stPreparationIntraOral",
            "stPreparation" => "stPreparationIntraOral",
            "stAntagonistModel" => "stAntagonistIntraOral",
            _ => scanType + "IntraOral",
        };
    }

    private static void SetProperty(XElement objectNode, string propertyName, string value)
    {
        XElement? property = objectNode.Elements("Property").FirstOrDefault(x => (string?)x.Attribute("name") == propertyName);
        if (property is null)
        {
            objectNode.Add(NewProperty(propertyName, value));
            return;
        }

        property.SetAttributeValue("value", value);
    }

    private static XElement NewProperty(string name, string value)
    {
        return new XElement("Property", new XAttribute("name", name), new XAttribute("value", value));
    }
}
