using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class DtePorPeriodoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/por-periodo", async (
            DteDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? agrupar,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = agrupar switch
            {
                "dia" => """
                    SELECT d.fecha_emision::date AS Periodo,
                           COUNT(DISTINCT d.id) AS TotalDtes,
                           ROUND(SUM(d.total)::numeric, 2) AS TotalMonto
                    FROM dtes d
                    WHERE d.tenant_id = @tenantId
                      AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                    GROUP BY d.fecha_emision::date
                    ORDER BY Periodo
                    """,
                "semana" => """
                    SELECT DATE_TRUNC('week', d.fecha_emision) AS Periodo,
                           COUNT(DISTINCT d.id) AS TotalDtes,
                           ROUND(SUM(d.total)::numeric, 2) AS TotalMonto
                    FROM dtes d
                    WHERE d.tenant_id = @tenantId
                      AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                    GROUP BY DATE_TRUNC('week', d.fecha_emision)
                    ORDER BY Periodo
                    """,
                _ => """
                    SELECT DATE_TRUNC('month', d.fecha_emision) AS Periodo,
                           COUNT(DISTINCT d.id) AS TotalDtes,
                           ROUND(SUM(d.total)::numeric, 2) AS TotalMonto
                    FROM dtes d
                    WHERE d.tenant_id = @tenantId
                      AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                    GROUP BY DATE_TRUNC('month', d.fecha_emision)
                    ORDER BY Periodo
                    """
            };

            var resultados = await db.Database.SqlQueryRaw<DtePeriodoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfDtePorPeriodo(db, tenantContext.TenantId, resultados, @de, @ha, agrupar);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfDtePorPeriodo(DteDbContext db, Guid? tenantId, List<DtePeriodoRow> data, DateTime desde, DateTime hasta, string? agrupar)
    {
        var totalDtes = data.Sum(r => r.TotalDtes);
        var totalMonto = data.Sum(r => r.TotalMonto);
        var agruparLabel = agrupar switch { "dia" => "por Dia", "semana" => "por Semana", _ => "por Mes" };

        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("DTE por Periodo");
        pdf.Empresa(empresa)
           .Reporte("DTE por Periodo")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy} - Agrupado {agruparLabel}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.Periodo.ToString("dd/MM/yyyy"),
            r.TotalDtes.ToString("N0"),
            r.TotalMonto.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Periodo", "DTEs Emitidos", "Monto Total"],
            rows: rows,
            totalRow: ["Total", totalDtes.ToString("N0"), totalMonto.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"dte-por-periodo-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record DtePeriodoRow(DateTime Periodo, long TotalDtes, decimal TotalMonto);

