namespace ANews.Domain.Entities;

public class ModuleKeyword : BaseEntity
{
    public required string Keyword { get; set; }
    public decimal Weight { get; set; } = 1.0m;
    public bool IsExact { get; set; } = false;
    public int UserModuleId { get; set; }

    public UserModule Module { get; set; } = null!;
}
