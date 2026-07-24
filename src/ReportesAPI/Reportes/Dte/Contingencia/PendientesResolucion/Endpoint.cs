using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Contingencia;

public static class PendientesResolucionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/contingencia/pendientes", async (DteDbContext db, TenantContext tenantContext, string? formato) =>
        {
            var sql = """
                SELECT d.id AS DteId,
                       d.codigo_generacion AS CodigoGeneracion,
                       d.tipo_dte AS TipoDte,
                       d.fecha_emision AS FechaEmision,
                       d.total,
                       d.fecha_emision AS ContingenciaDesde,
                       d.motivo_contingencia AS Motivo
                FROM dtes d
                WHERE d.tenant_id = @tenantId
                  AND d.estado = 'contingencia'
                ORDER BY d.fecha_emision ASC
                """;

            var resultados = await db.Database.SqlQueryRaw<PendienteResolucionRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!)
            ).ToListAsync();

            if (formato == "pdf") return await PdfPendientesResolucion(db, tenantContext.TenantId, resultados);

            return Results.Ok(new { datos = resultados, total_pendientes = resultados.Count });
        });
    }

    private static async Task<IResult> PdfPendientesResolucion(DteDbContext db, Guid? tenantId, List<PendienteResolucionRow> data)
    {
        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("Pendientes de Resolucion");
        pdf.Empresa(empresa)
           .Reporte("Pendientes de Resolucion")
           .Periodo("Pendientes actuales")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.CodigoGeneracion,
            r.TipoDte,
            r.Motivo ?? "",
            r.FechaEmision.ToString("dd/MM/yyyy"),
            (DateTime.Now - r.ContingenciaDesde).Days.ToString("N0")
        });

        pdf.Tabla(
            headers: ["Codigo DTE", "Tipo", "Evento", "Fecha", "Dias Pendiente"],
            rows: rows
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"pendientes-resolucion-{DateTime.Now:yyyyMMdd}.pdf");
    }
}

public record PendienteResolucionRow(Guid DteId, string CodigoGeneracion, string TipoDte, DateTime FechaEmision, decimal Total, DateTime ContingenciaDesde, string? Motivo);
