using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Costos;

public static class MargenEvolucionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/costos/margen-evolucion", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? agrupar,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddDays(-30);
            var @ha = hasta ?? DateTime.Today;
            var agruparPor = agrupar ?? "dia";

            var sql = agruparPor switch
            {
                "semana" => """
                    SELECT
                        DATE_TRUNC('week', DATE(o.pagado_en))::date AS Fecha,
                        COUNT(DISTINCT o.id)::bigint AS Ordenes,
                        ROUND(CAST(SUM(o.total) AS NUMERIC), 2) AS Ingresos
                    FROM ordenes o
                    WHERE o.tenant_id = @tenantId AND o.estado = 'pagada'
                        AND o.pagado_en >= @de AND o.pagado_en < @ha::date + 1
                    GROUP BY DATE_TRUNC('week', DATE(o.pagado_en))::date
                    ORDER BY Fecha ASC
                    """,
                "mes" => """
                    SELECT
                        DATE_TRUNC('month', DATE(o.pagado_en))::date AS Fecha,
                        COUNT(DISTINCT o.id)::bigint AS Ordenes,
                        ROUND(CAST(SUM(o.total) AS NUMERIC), 2) AS Ingresos
                    FROM ordenes o
                    WHERE o.tenant_id = @tenantId AND o.estado = 'pagada'
                        AND o.pagado_en >= @de AND o.pagado_en < @ha::date + 1
                    GROUP BY DATE_TRUNC('month', DATE(o.pagado_en))::date
                    ORDER BY Fecha ASC
                    """,
                _ => """
                    SELECT
                        DATE(o.pagado_en) AS Fecha,
                        COUNT(DISTINCT o.id)::bigint AS Ordenes,
                        ROUND(CAST(SUM(o.total) AS NUMERIC), 2) AS Ingresos
                    FROM ordenes o
                    WHERE o.tenant_id = @tenantId AND o.estado = 'pagada'
                        AND o.pagado_en >= @de AND o.pagado_en < @ha::date + 1
                    GROUP BY DATE(o.pagado_en)
                    ORDER BY Fecha ASC
                    """
            };

            var resultados = await db.Database.SqlQueryRaw<MargenEvolucionRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfMargenEvolucion(resultados, agruparPor, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha, agrupar });
        });
    }

    private static async Task<IResult> PdfMargenEvolucion(List<MargenEvolucionRow> data, string agrupar, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var sumOrdenes = data.Sum(r => r.Ordenes);
        var sumIngresos = data.Sum(r => r.Ingresos);
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        var etiqueta = agrupar switch { "semana" => "Semana", "mes" => "Mes", _ => "Diario" };

        using var pdf = new PdfBuilder();
        pdf.Titulo("Evolución de Ingresos");
        pdf.Empresa(empresa)
           .Reporte($"Evolución de Ingresos — {etiqueta}")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.Fecha.ToString("dd/MM/yyyy"),
            r.Ordenes.ToString("N0"),
            r.Ingresos.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Fecha", "Órdenes", "Ingresos"],
            rows: rows,
            totalRow: ["Total", sumOrdenes.ToString("N0"), sumIngresos.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"margen-evolucion-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record MargenEvolucionRow(DateTime Fecha, long Ordenes, decimal Ingresos);
