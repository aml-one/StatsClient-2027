using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text.RegularExpressions;


public partial class FDIConverter
{
    public static void ConvertXMLFromFDI(string XMLFile)
    {
        try
        {
            bool isItFDI = false;
            if (File.Exists(XMLFile))
            {
                XmlDocument doc = new ();
                doc.Load(XMLFile);

                XmlElement root = doc.DocumentElement!;

                List<XmlNode> nodes = [];

                foreach (XmlNode node in root.ChildNodes[0]!.ChildNodes)
                {
                    if (!nodes.Contains(node))
                        nodes.Add(node);
                }


                #region Getting tooth numbers from ModelElementList
                XmlNode TDM_List_ModelElement_XMLNode = nodes.Single(x => x.Attributes!["name"]!.Value == "ModelElementList");

                Dictionary<string, string> ModelElements = [];

                foreach (XmlNode node in TDM_List_ModelElement_XMLNode.ChildNodes)
                {
                    // extracting ModelJobIDs
                    foreach (XmlNode objectNode in node.ChildNodes)
                    {
                        string ModelElementID = "";
                        string Items = "";

                        foreach (XmlNode property in objectNode.ChildNodes)
                        {
                            if (property.Attributes!["name"]!.Value == "ModelElementID")
                                ModelElementID = property.Attributes["value"]!.Value;

                            if (property.Attributes["name"]!.Value == "Items")
                                Items = property.Attributes["value"]!.Value;

                            if (ModelElementID.Length > 0 && Items.Length > 0)
                            {
                                // only collecting entries where we got number in the line and only storing the numbers from it
                                Regex regx = FDIRegex();
                                Match match = regx.Match(Items);
                                if (match.Success && !ModelElements.ContainsKey(ModelElementID))
                                {
                                    string ItemHelper = string.Empty;
                                    string ToothNumber = Items;

                                    for (int i = 0; i < Items.Length; i++)
                                    {
                                        if (Char.IsDigit(Items[i]) || Items[i].ToString() == "-")
                                            ItemHelper += Items[i];
                                    }

                                    if (ItemHelper.Length > 0)
                                        ToothNumber = ItemHelper;

                                    ModelElements.Add(ModelElementID, ToothNumber);
                                }
                                continue;
                            }
                        }
                    }
                }
                #endregion  Getting tooth numbers from ModelElementList


                #region Getting original tooth numbers from ToothElementList

                XmlNode TDM_List_ToothElement_XMLNode = nodes.Single(x => x.Attributes!["name"]!.Value == "ToothElementList");

                Dictionary<string, string> ToothElementList = [];

                foreach (XmlNode node in TDM_List_ToothElement_XMLNode.ChildNodes)
                {
                    // extracting ModelJobIDs
                    foreach (XmlNode objectNode in node.ChildNodes)
                    {
                        string ModelElementID = "";
                        foreach (XmlNode property in objectNode.ChildNodes)
                        {
                            if (property.Attributes!["name"]!.Value == "ModelElementID")
                            {
                                if (!ModelElements.ContainsKey(property.Attributes["value"]!.Value))
                                    continue; // if the current object's ModelElementID is not in the previously built list, then skipping and checking the next object

                                ModelElementID = property.Attributes["value"]!.Value;
                            }

                            if (property.Attributes["name"]!.Value == "ToothNumber")
                            {
                                if (!ToothElementList.ContainsKey(ModelElementID))
                                    ToothElementList.Add(ModelElementID, property.Attributes["value"]!.Value);
                            }
                        }
                    }
                }
                #endregion Getting original tooth numbers from ToothElementList


                #region Fixing up XML & saving down

                TDM_List_ModelElement_XMLNode = nodes.Single(x => x.Attributes!["name"]!.Value == "ModelElementList");

                foreach (XmlNode node in TDM_List_ModelElement_XMLNode.ChildNodes)
                {
                    foreach (XmlNode objectNode in node.ChildNodes)
                    {
                        string ModelElementID = "";
                        
                        foreach (XmlNode property in objectNode.ChildNodes)
                        {
                            if (property.Attributes!["name"]!.Value == "ModelElementID")
                                ModelElementID = property.Attributes["value"]!.Value;

                            if (property.Attributes["name"]!.Value == "Items")
                            {
                                if (ModelElements.ContainsKey(ModelElementID) && ToothElementList.ContainsKey(ModelElementID))
                                {
                                    if (ModelElements[ModelElementID].Contains('-'))
                                    {
                                        if (!ModelElements[ModelElementID].StartsWith(ToothElementList[ModelElementID]))
                                            isItFDI = true;

                                        //bridges
                                        property.Attributes["value"]!.Value = property.Attributes["value"]!.Value.Replace(ModelElements[ModelElementID], ConvertFDIinString(ModelElements[ModelElementID]));
                                    }
                                    else
                                    {
                                        if (ModelElements[ModelElementID] != ToothElementList[ModelElementID])
                                            isItFDI = true;

                                        //single units
                                        property.Attributes["value"]!.Value = property.Attributes["value"]!.Value.Replace(ModelElements[ModelElementID], ToothElementList[ModelElementID]);
                                    }
                                }

                            }
                        }
                    }
                }


                #region Fixing Items in OrderList if it's FDI Notation
                if (isItFDI)
                {
                    XmlNode TDM_List_Order_XMLNode = nodes.Single(x => x.Attributes!["name"]!.Value == "OrderList");

                    foreach (XmlNode property in TDM_List_Order_XMLNode.ChildNodes[0]!.ChildNodes[0]!)
                    {
                        if (property.Attributes!["name"]!.Value == "Items")
                            property.Attributes["value"]!.Value = ConvertFDIinString(property.Attributes["value"]!.Value);

                        if (property.Attributes["name"]!.Value == "OrderComments")
                            property.Attributes["value"]!.Value = System.Net.WebUtility.HtmlDecode(property.Attributes["value"]!.Value + "&#xA;[Converted From FDI]");
                    }
                }
                #endregion Fixing Items in OrderList


                doc.Save(XMLFile);

                #endregion Fixing up XML & saving down

            }
        }
        catch
        {
        }
    }

    public static void ConvertXMLFromFDI(string XMLFile, bool SaveDummyFile)
    {
        try
        {
            bool isItFDI = false;
            if (File.Exists(XMLFile))
            {
                XmlDocument doc = new ();
                doc.Load(XMLFile);

                XmlElement root = doc.DocumentElement!;

                List<XmlNode> nodes = [];

                foreach (XmlNode node in root.ChildNodes[0]!.ChildNodes)
                {
                    if (!nodes.Contains(node))
                        nodes.Add(node);
                }


                #region Getting tooth numbers from ModelElementList
                XmlNode TDM_List_ModelElement_XMLNode = nodes.Single(x => x.Attributes!["name"]!.Value == "ModelElementList");

                Dictionary<string, string> ModelElements = new Dictionary<string, string>();

                foreach (XmlNode node in TDM_List_ModelElement_XMLNode.ChildNodes)
                {
                    // extracting ModelJobIDs
                    foreach (XmlNode objectNode in node.ChildNodes)
                    {
                        string ModelElementID = "";
                        string Items = "";

                        foreach (XmlNode property in objectNode.ChildNodes)
                        {
                            if (property.Attributes!["name"]!.Value == "ModelElementID")
                                ModelElementID = property.Attributes["value"]!.Value;

                            if (property.Attributes["name"]!.Value == "Items")
                                Items = property.Attributes["value"]!.Value;

                            if (ModelElementID.Length > 0 && Items.Length > 0)
                            {
                                // only collecting entries where we got number in the line and only storing the numbers from it
                                Regex regx = FDIRegex();
                                Match match = regx.Match(Items);
                                if (match.Success && !ModelElements.ContainsKey(ModelElementID))
                                {
                                    string ItemHelper = string.Empty;
                                    string ToothNumber = Items;

                                    for (int i = 0; i < Items.Length; i++)
                                    {
                                        if (char.IsDigit(Items[i]) || Items[i].ToString() == "-")
                                            ItemHelper += Items[i];
                                    }

                                    if (ItemHelper.Length > 0)
                                        ToothNumber = ItemHelper;

                                    ModelElements.Add(ModelElementID, ToothNumber);
                                }
                                continue;
                            }
                        }
                    }
                }
                #endregion  Getting tooth numbers from ModelElementList


                #region Getting original tooth numbers from ToothElementList

                XmlNode TDM_List_ToothElement_XMLNode = nodes.Single(x => x.Attributes!["name"]!.Value == "ToothElementList");

                Dictionary<string, string> ToothElementList = [];

                foreach (XmlNode node in TDM_List_ToothElement_XMLNode.ChildNodes)
                {
                    // extracting ModelJobIDs
                    foreach (XmlNode objectNode in node.ChildNodes)
                    {
                        string ModelElementID = "";
                        foreach (XmlNode property in objectNode.ChildNodes)
                        {
                            if (property.Attributes!["name"]!.Value == "ModelElementID")
                            {
                                if (!ModelElements.ContainsKey(property.Attributes["value"]!.Value))
                                    continue; // if the current object's ModelElementID is not in the previously built list, then skipping and checking the next object

                                ModelElementID = property.Attributes["value"]!.Value;
                            }

                            if (property.Attributes["name"]!.Value == "ToothNumber")
                            {
                                if (!ToothElementList.ContainsKey(ModelElementID))
                                    ToothElementList.Add(ModelElementID, property.Attributes["value"]!.Value);
                            }
                        }
                    }
                }
                #endregion Getting original tooth numbers from ToothElementList


                #region Fixing up XML & saving down

                TDM_List_ModelElement_XMLNode = nodes.Single(x => x.Attributes!["name"]!.Value == "ModelElementList");

                foreach (XmlNode node in TDM_List_ModelElement_XMLNode.ChildNodes)
                {
                    foreach (XmlNode objectNode in node.ChildNodes)
                    {
                        string ModelElementID = "";
                        
                        foreach (XmlNode property in objectNode.ChildNodes)
                        {
                            if (property.Attributes!["name"]!.Value == "ModelElementID")
                                ModelElementID = property.Attributes["value"]!.Value;

                            if (property.Attributes["name"]!.Value == "Items")
                            {
                                if (ModelElements.ContainsKey(ModelElementID) && ToothElementList.ContainsKey(ModelElementID))
                                {
                                    if (ModelElements[ModelElementID].Contains('-'))
                                    {
                                        if (!ModelElements[ModelElementID].StartsWith(ToothElementList[ModelElementID]))
                                            isItFDI = true;

                                        //bridges
                                        property.Attributes["value"]!.Value = property.Attributes["value"]!.Value.Replace(ModelElements[ModelElementID], ConvertFDIinString(ModelElements[ModelElementID]));
                                    }
                                    else
                                    {
                                        if (ModelElements[ModelElementID] != ToothElementList[ModelElementID])
                                            isItFDI = true;

                                        //single units
                                        property.Attributes["value"]!.Value = property.Attributes["value"]!.Value.Replace(ModelElements[ModelElementID], ToothElementList[ModelElementID]);
                                    }
                                }

                            }
                        }
                    }
                }


                #region Fixing Items in OrderList if it's FDI Notation
                if (isItFDI)
                {
                    XmlNode TDM_List_Order_XMLNode = nodes.Single(x => x.Attributes!["name"]!.Value == "OrderList");

                    foreach (XmlNode property in TDM_List_Order_XMLNode.ChildNodes[0]!.ChildNodes[0]!)
                    {
                        if (property.Attributes!["name"]!.Value == "Items")
                            property.Attributes["value"]!.Value = ConvertFDIinString(property.Attributes["value"]!.Value);

                        if (property.Attributes["name"]!.Value == "OrderComments")
                            property.Attributes["value"]!.Value = System.Net.WebUtility.HtmlDecode(property.Attributes["value"]!.Value + "&#xA;[Converted From FDI]");
                    }
                }
                #endregion Fixing Items in OrderList

                if (SaveDummyFile)
                    doc.Save(XMLFile + ".new");
                else
                    doc.Save(XMLFile);

                #endregion Fixing up XML & saving down

            }
        }
        catch
        {
        }
    }



    public static string ConvertFDIinString(string Text)
    {
        string content = Text;
        string contentHelper = Text;
        string result = "";
        Regex regx = new Regex(@"\d+");
        Match match = regx.Match(Text);

        while (match.Success)
        {
            content = content.Substring(match.Index + match.Value.Length);
            result += contentHelper.Substring(0, match.Index) + ConvertFDIToUniversalNumeric(match.Value);
            contentHelper = contentHelper.Substring(match.Index + match.Value.Length);
            match = regx.Match(content);
        }

        return result;
    }

    public static string ConvertFDIToUniversalNumeric(string ToothNumber)
    {
        switch (ToothNumber)
        {
            case "18": return "1";
            case "17": return "2";
            case "16": return "3";
            case "15": return "4";
            case "14": return "5";
            case "13": return "6";
            case "12": return "7";
            case "11": return "8";

            case "21": return "9";
            case "22": return "10";
            case "23": return "11";
            case "24": return "12";
            case "25": return "13";
            case "26": return "14";
            case "27": return "15";
            case "28": return "16";

            case "38": return "17";
            case "37": return "18";
            case "36": return "19";
            case "35": return "20";
            case "34": return "21";
            case "33": return "22";
            case "32": return "23";
            case "31": return "24";

            case "41": return "25";
            case "42": return "26";
            case "43": return "27";
            case "44": return "28";
            case "45": return "29";
            case "46": return "30";
            case "47": return "31";
            case "48": return "32";

            default: return "";
        }
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex FDIRegex();
}

