namespace ANews.Domain.Entities;

/// <summary>
/// Informe generado por IA a partir del prompt de un modulo de usuario.
/// Cada ejecucion del agente genera un nuevo informe consultable.
/// </summary>
public class ModuleReport : BaseEntity
{
    public int UserModuleId { get; set; }
    public int UserId { get; set; }

    /// <summary>
    /// Titulo generado automaticamente por la IA para el informe.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Contenido completo del informe en formato Markdown.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Resumen corto (1-2 frases) para mostrar en tarjetas.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// IDs de los eventos que la IA uso como fuente para el informe.
    /// </summary>
    public List<int> SourceEventIds { get; set; } = [];

    /// <summary>
    /// Numero de eventos analizados para generar el informe.
    /// </summary>
    public int EventsAnalyzed { get; set; }

    /// <summary>
    /// Periodo que cubre el informe.
    /// </summary>
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public UserModule Module { get; set; } = null!;
}
