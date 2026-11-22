namespace StatsClient.MVVM.Core.ScanBodyGallery;



public static class ScanBodyGalleryTagHelper

{

    public static List<string> BuildTags(

        string brand,

        string systemName,

        string sizeLabel,

        string otherInfo,

        IEnumerable<string> manualTags)

    {

        var tags = new List<string>();



        foreach (string value in new[] { brand, systemName, sizeLabel })

        {

            string trimmed = value.Trim();

            if (!string.IsNullOrWhiteSpace(trimmed))

                tags.Add(trimmed);

        }



        tags.AddRange(Tokenize(otherInfo));

        tags.AddRange(NormalizeTags(manualTags));



        return tags

            .Distinct(StringComparer.OrdinalIgnoreCase)

            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)

            .ToList();

    }



    public static List<string> NormalizeTags(IEnumerable<string> tags) =>

        tags

            .SelectMany(t => t.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))

            .Select(t => t.Trim())

            .Where(t => !string.IsNullOrWhiteSpace(t))

            .Distinct(StringComparer.OrdinalIgnoreCase)

            .ToList();



    private static IEnumerable<string> Tokenize(string text)

    {

        if (string.IsNullOrWhiteSpace(text))

            return [];



        return text

            .Split([' ', ',', ';', '/', '\\', '|', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)

            .Where(t => t.Length >= 2)

            .Select(t => t.Trim());

    }

}

