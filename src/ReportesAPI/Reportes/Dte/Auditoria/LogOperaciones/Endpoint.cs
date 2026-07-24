using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Auditoria;

public static class LogOperacionesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/auditoria/operaciones", async (
            DteDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            int? limite,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddDays(-7);
            var @ha = hasta ?? DateTime.Today;
            var top = limite ?? 100;

            var sql = """
                SELECT a.id,
                       a.creado_en AS Fecha,
                       a.usuario_id AS UsuarioId,
                       u.nombre AS UsuarioNombre,
                       a.accion,
                       a.tabla_afectada AS TablaAfectada,
                       a.registro_id AS RegistroId,
                       a.ip_origen AS Detalle
                FROM auditoria a
                LEFT JOIN usuarios u ON u.id = a.usuario_id
                WHERE a.tenant_id = @tenantId
                  AND a.creado_en >= @de AND a.creado_en < @ha::date + 1
                ORDER BY a.creado_en DESC
                LIMIT @top
                """;

            var resultados = await db.Database.SqlQueryRaw<LogOperacionRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha),
                new NpgsqlParameter("@top", top)
            ).ToListAsync();

            if (formato == "pdf") return await PdfLogOperaciones(db, tenantContext.TenantId, resultados, @de, @ha);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfLogOperaciones(DteDbContext db, Guid? tenantId, List<LogOperacionRow> data, DateTime desde, DateTime hasta)
    {
        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("Log de Operaciones");
        pdf.Empresa(empresa)
           .Reporte("Log de Operaciones")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.Fecha.ToString("dd/MM/yyyy HH:mm"),
            r.UsuarioNombre ?? "",
            r.Accion,
            r.Detalle ?? "",
            ""
        });

        pdf.Tabla(
            headers: ["Fecha", "Usuario", "Accion", "Detalle", "IP"],
            rows: rows
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"log-operaciones-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record LogOperacionRow(Guid Id, DateTime Fecha, Guid? UsuarioId, string? UsuarioNombre, string Accion, string? TablaAfectada, Guid? RegistroId, string? Detalle);
