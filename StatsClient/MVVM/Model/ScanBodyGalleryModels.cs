namespace StatsClient.MVVM.Model;



public class ScanBodyGalleryEntryModel

{

    public int EntryId { get; set; }

    public string Brand { get; set; } = string.Empty;

    public string SystemName { get; set; } = string.Empty;

    public string SizeLabel { get; set; } = string.Empty;

    public string OtherInfo { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public string TagsDisplay => string.Join(", ", Tags);

    public string DisplayTitle =>

        string.Join(" · ", new[] { Brand, SystemName, SizeLabel }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public int SortOrder { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public byte[]? ThumbnailData { get; set; }
    public bool HasKnowledgeBaseMatch { get; set; }
    public int KnowledgeBaseMatchCount { get; set; }
}



public class ScanBodyGalleryImageModel

{

    public int ImageId { get; set; }

    public int EntryId { get; set; }

    public string FileName { get; set; } = "image.png";

    public string ContentType { get; set; } = "image/png";

    public byte[] ImageData { get; set; } = [];

    public byte[]? ThumbnailData { get; set; }

    public int SortOrder { get; set; }

}



public class ScanBodyGalleryEntryDetailModel

{

    public int EntryId { get; set; }

    public string Brand { get; set; } = string.Empty;

    public string SystemName { get; set; } = string.Empty;

    public string SizeLabel { get; set; } = string.Empty;

    public string OtherInfo { get; set; } = string.Empty;

    public string ManualTagsText { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public int SortOrder { get; set; }

    public List<ScanBodyGalleryImageModel> Images { get; set; } = [];

}



public sealed class ScanBodyGalleryVisionMatch

{

    public int EntryId { get; set; }

    public string Title { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public string Reason { get; set; } = string.Empty;



    public int ConfidencePercent => (int)Math.Round(NormalizeConfidence(Confidence) * 100);



    public string ConfidenceDisplay => $"{ConfidencePercent}% match";



    public static double NormalizeConfidence(double value) =>

        value > 1.0 ? Math.Clamp(value / 100.0, 0, 1) : Math.Clamp(value, 0, 1);

}



public sealed class ScanBodyGalleryImageEditorItem

{

    public int ImageId { get; set; }

    public byte[] ImageData { get; set; } = [];

    public byte[]? ThumbnailData { get; set; }

    public string FileName { get; set; } = "image.png";

    public string ContentType { get; set; } = "image/png";

}

