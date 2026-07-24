using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ventas;

public static class VentasPorPeriodoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ventas/por-periodo", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? agrupar,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = agrupar switch
            {
                "dia" => """
                    SELECT DATE(o.creado_en) AS Periodo,
                           COUNT(DISTINCT o.id) AS TotalOrdenes,
                           SUM(o.total) AS TotalIngresos
                    FROM ordenes o
                    WHERE o.tenant_id = @tenantId
                      AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                      AND o.estado = 'pagada'
                    GROUP BY DATE(o.creado_en)
                    ORDER BY Periodo
                    """,
                "semana" => """
                    SELECT DATE_TRUNC('week', o.creado_en) AS Periodo,
                           COUNT(DISTINCT o.id) AS TotalOrdenes,
                           SUM(o.total) AS TotalIngresos
                    FROM ordenes o
                    WHERE o.tenant_id = @tenantId
                      AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                      AND o.estado = 'pagada'
                    GROUP BY DATE_TRUNC('week', o.creado_en)
                    ORDER BY Periodo
                    """,
                "mes" => """
                    SELECT DATE_TRUNC('month', o.creado_en) AS Periodo,
                           COUNT(DISTINCT o.id) AS TotalOrdenes,
                           SUM(o.total) AS TotalIngresos
                    FROM ordenes o
                    WHERE o.tenant_id = @tenantId
                      AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                      AND o.estado = 'pagada'
                    GROUP BY DATE_TRUNC('month', o.creado_en)
                    ORDER BY Periodo
                    """,
                _ => """
                    SELECT o.creado_en::date AS Periodo,
                           COUNT(DISTINCT o.id) AS TotalOrdenes,
                           SUM(o.total) AS TotalIngresos
                    FROM ordenes o
                    WHERE o.tenant_id = @tenantId
                      AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                      AND o.estado = 'pagada'
                    GROUP BY o.creado_en::date
                    ORDER BY Periodo
                    """
            };

            var resultados = await db.Database.SqlQueryRaw<VentasPeriodoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfVentasPorPeriodo(resultados, @de, @ha, agrupar, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfVentasPorPeriodo(List<VentasPeriodoRow> data, DateTime desde, DateTime hasta, string? agrupar, PosDbContext db, Guid? tenantId)
    {
        var totalOrdenes = data.Sum(r => r.TotalOrdenes);
        var totalIngresos = data.Sum(r => r.TotalIngresos);
        var agruparLabel = agrupar switch { "semana" => "por Semana", "mes" => "por Mes", _ => "por Dia" };
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo($"Ventas {agruparLabel}");
        pdf.Empresa(empresa)
           .Reporte("Ventas por Periodo")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy} - Agrupado {agruparLabel}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.Periodo.ToString("dd/MM/yyyy"),
            r.TotalOrdenes.ToString("N0"),
            r.TotalIngresos.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Periodo", "Ordenes", "Ingresos"],
            rows: rows,
            totalRow: ["Total", totalOrdenes.ToString("N0"), totalIngresos.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");

        return Results.File(pdf.Generar(), "application/pdf", $"ventas-por-periodo-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record VentasPeriodoRow(DateTime Periodo, long TotalOrdenes, decimal TotalIngresos);

