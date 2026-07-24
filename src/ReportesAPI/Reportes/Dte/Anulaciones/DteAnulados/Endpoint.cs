using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Anulaciones;

public static class DteAnuladosEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/anulaciones", async (
            DteDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-3);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT d.id AS DteId,
                       d.codigo_generacion AS CodigoGeneracion,
                       d.tipo_dte AS TipoDte,
                       d.fecha_emision AS FechaEmision,
                       d.total,
                       d.fecha_anulacion AS FechaAnulacion,
                       d.motivo_anulacion AS MotivoAnulacion
                FROM dtes d
                WHERE d.tenant_id = @tenantId
                  AND d.estado = 'anulado'
                  AND d.fecha_anulacion >= @de AND d.fecha_anulacion < @ha::date + 1
                ORDER BY d.fecha_anulacion DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DteAnuladoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf") return await PdfDteAnulados(db, tenantContext.TenantId, resultados, @de, @ha);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfDteAnulados(DteDbContext db, Guid? tenantId, List<DteAnuladoRow> data, DateTime desde, DateTime hasta)
    {
        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("DTE Anulados");
        pdf.Empresa(empresa)
           .Reporte("DTE Anulados")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.CodigoGeneracion,
            r.TipoDte,
            r.FechaAnulacion?.ToString("dd/MM/yyyy") ?? "",
            r.MotivoAnulacion ?? "",
            r.Total.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Codigo", "Tipo", "Fecha Anulacion", "Motivo", "Monto"],
            rows: rows
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"dte-anulados-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record DteAnuladoRow(Guid DteId, string CodigoGeneracion, string TipoDte, DateTime FechaEmision, decimal Total, DateTime? FechaAnulacion, string? MotivoAnulacion);
