namespace MyDigitalLibrary.Core.Models;

public class GoogleBook
{
    public string Id { get; set; } = string.Empty;

    public VolumeInfoData VolumeInfo { get; set; } = new VolumeInfoData();

    public class VolumeInfoData
    {
        public string? Title { get; set; }
        public string[]? Authors { get; set; }
        public string? Publisher { get; set; }
        public string? PublishedDate { get; set; }
        public string? Description { get; set; }
        public IndustryIdentifier[]? IndustryIdentifiers { get; set; }
        public int? PageCount { get; set; }
        public string? Language { get; set; }
        public string? ThumbnailLink { get; set; }
        public string? SmallThumbnailLink { get; set; }
    }

    public class IndustryIdentifier
    {
        public string Type { get; set; } = string.Empty; // e.g. ISBN_10, ISBN_13
        public string Identifier { get; set; } = string.Empty;
    }
}
