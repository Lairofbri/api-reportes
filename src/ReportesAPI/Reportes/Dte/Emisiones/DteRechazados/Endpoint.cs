using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class DteRechazadosEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/rechazados", async (
            DteDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT d.id AS DteId,
                       d.codigo_generacion AS CodigoGeneracion,
                       d.tipo_dte AS TipoDte,
                       d.fecha_emision AS FechaEmision,
                       d.total,
                       d.motivo_rechazo AS Observaciones,
                       d.estado
                FROM dtes d
                WHERE d.tenant_id = @tenantId
                  AND d.estado = 'rechazado'
                  AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                ORDER BY D.fecha_emision DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DteRechazadoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf") return await PdfDteRechazados(db, tenantContext.TenantId, resultados, @de, @ha);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfDteRechazados(DteDbContext db, Guid? tenantId, List<DteRechazadoRow> data, DateTime desde, DateTime hasta)
    {
        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("DTE Rechazados");
        pdf.Empresa(empresa)
           .Reporte("DTE Rechazados")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.CodigoGeneracion,
            r.TipoDte,
            r.FechaEmision.ToString("dd/MM/yyyy"),
            r.Observaciones ?? "",
            r.Total.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Codigo", "Tipo", "Fecha", "Motivo", "Monto"],
            rows: rows
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"dte-rechazados-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record DteRechazadoRow(Guid DteId, string CodigoGeneracion, string TipoDte, DateTime FechaEmision, decimal Total, string? Observaciones, string Estado);
