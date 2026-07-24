namespace ReportesAPI.Compartido.MultiTenant;

public class TenantContext
{
    public Guid? TenantId { get; set; }
    public string AuthMode { get; set; } = "unknown";
    public string? UsuarioId { get; set; }
    public string? Rol { get; set; }
    public string? Email { get; set; }
    public Guid? SucursalId { get; set; }
    public Guid? EstablecimientoId { get; set; }
}
