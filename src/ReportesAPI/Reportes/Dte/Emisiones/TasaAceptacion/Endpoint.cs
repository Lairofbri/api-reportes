using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class TasaAceptacionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/tasa-aceptacion", async (
            DteDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT DATE_TRUNC('month', d.fecha_emision) AS Periodo,
                       COUNT(DISTINCT d.id) FILTER (WHERE d.estado = 'aceptado') AS Aceptados,
                       COUNT(DISTINCT d.id) FILTER (WHERE d.estado = 'rechazado') AS Rechazados,
                       COUNT(DISTINCT d.id) FILTER (WHERE d.estado = 'contingencia') AS Contingencias,
                       COUNT(DISTINCT d.id) AS Total,
                       ROUND(
                           (COUNT(DISTINCT d.id) FILTER (WHERE d.estado = 'aceptado') * 100.0 /
                           NULLIF(COUNT(DISTINCT d.id), 0))::numeric, 2
                       ) AS PorcentajeAceptacion
                FROM dtes d
                WHERE d.tenant_id = @tenantId
                  AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                GROUP BY DATE_TRUNC('month', d.fecha_emision)
                ORDER BY Periodo
                """;

            var resultados = await db.Database.SqlQueryRaw<TasaAceptacionRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf") return await PdfTasaAceptacion(db, tenantContext.TenantId, resultados, @de, @ha);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfTasaAceptacion(DteDbContext db, Guid? tenantId, List<TasaAceptacionRow> data, DateTime desde, DateTime hasta)
    {
        var totalAceptados = data.Sum(r => r.Aceptados);
        var totalRechazados = data.Sum(r => r.Rechazados);
        var totalContingencias = data.Sum(r => r.Contingencias);
        var totalGeneral = data.Sum(r => r.Total);
        var tasaGlobal = totalGeneral > 0
            ? Math.Round((double)totalAceptados / totalGeneral * 100, 2)
            : 0;

        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("Tasa de Aceptacion");
        pdf.Empresa(empresa)
           .Reporte("Tasa de Aceptacion")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.Periodo.ToString("MM/yyyy"),
            r.Aceptados.ToString("N0"),
            r.Rechazados.ToString("N0"),
            r.Contingencias.ToString("N0"),
            r.PorcentajeAceptacion.ToString("N2") + "%"
        });

        pdf.Tabla(
            headers: ["Periodo", "Aceptados", "Rechazados", "Contingencia", "Tasa"],
            rows: rows,
            totalRow: ["Total", totalAceptados.ToString("N0"), totalRechazados.ToString("N0"), totalContingencias.ToString("N0"), tasaGlobal.ToString("N2") + "%"]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"tasa-aceptacion-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record TasaAceptacionRow(DateTime Periodo, long Aceptados, long Rechazados, long Contingencias, long Total, decimal PorcentajeAceptacion);
