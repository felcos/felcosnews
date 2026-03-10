namespace ANews.Domain.Entities;

public class WorkspaceZone : BaseEntity
{
    public required string Name { get; set; }
    public string Flag { get; set; } = "🌍";
    public string Description { get; set; } = "";
    /// <summary>Terms used to filter events geographically (jsonb array)</summary>
    public List<string> GeoTerms { get; set; } = [];
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}
