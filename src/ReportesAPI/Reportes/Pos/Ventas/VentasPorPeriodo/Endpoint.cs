using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ventas;

public static class VentasPorPeriodoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ventas/por-periodo", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta,
            string? agrupar) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = agrupar switch
            {
                "dia" => """
                    SELECT DATE(o.creado_en) AS periodo,
                           COUNT(DISTINCT o.id) AS total_ordenes,
                           SUM(o.total) AS total_ingresos
                    FROM ordenes o
                    WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                      AND o.estado = 'pagada'
                    GROUP BY DATE(o.creado_en)
                    ORDER BY periodo
                    """,
                "semana" => """
                    SELECT DATE_TRUNC('week', o.creado_en) AS periodo,
                           COUNT(DISTINCT o.id) AS total_ordenes,
                           SUM(o.total) AS total_ingresos
                    FROM ordenes o
                    WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                      AND o.estado = 'pagada'
                    GROUP BY DATE_TRUNC('week', o.creado_en)
                    ORDER BY periodo
                    """,
                "mes" => """
                    SELECT DATE_TRUNC('month', o.creado_en) AS periodo,
                           COUNT(DISTINCT o.id) AS total_ordenes,
                           SUM(o.total) AS total_ingresos
                    FROM ordenes o
                    WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                      AND o.estado = 'pagada'
                    GROUP BY DATE_TRUNC('month', o.creado_en)
                    ORDER BY periodo
                    """,
                _ => """
                    SELECT o.creado_en::date AS periodo,
                           COUNT(DISTINCT o.id) AS total_ordenes,
                           SUM(o.total) AS total_ingresos
                    FROM ordenes o
                    WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                      AND o.estado = 'pagada'
                    GROUP BY o.creado_en::date
                    ORDER BY periodo
                    """
            };

            var resultados = await db.Database.SqlQueryRaw<VentasPeriodoRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record VentasPeriodoRow(DateTime Periodo, long TotalOrdenes, decimal TotalIngresos);

