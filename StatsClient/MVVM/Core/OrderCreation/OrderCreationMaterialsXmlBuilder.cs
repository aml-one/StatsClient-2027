using System.Xml.Linq;

namespace StatsClient.MVVM.Core.OrderCreation;

internal sealed class OrderCreationMaterialsXmlBuilder
{
    public XDocument Build(OrderCreationInput input, string globalMaterialsXmlPath)
    {
        XDocument source = XDocument.Load(globalMaterialsXmlPath);
        XDocument output = new(source);

        XElement? materialItems = output
            .Descendants("Object")
            .FirstOrDefault(x => (string?)x.Attribute("name") == "MaterialList")?
            .Element("List");

        if (materialItems is null)
        {
            throw new InvalidOperationException("MaterialList not found in global materials XML.");
        }

        foreach (XElement objectNode in output.Descendants("Object").Where(x => (string?)x.Attribute("name") != "MaterialList"))
        {
            XElement? list = objectNode.Element("List");
            if (list is null)
            {
                continue;
            }

            list.RemoveNodes();
        }

        return output;
    }
}
