using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class DtePorPeriodoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/por-periodo", async (
            DteDbContext db,
            DateTime? desde,
            DateTime? hasta,
            string? agrupar) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = agrupar switch
            {
                "dia" => """
                    SELECT d.fecha_emision::date AS periodo,
                           COUNT(DISTINCT d.id) AS total_dtes,
                           ROUND(SUM(d.total)::numeric, 2) AS total_monto
                    FROM dtes d
                    WHERE d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                    GROUP BY d.fecha_emision::date
                    ORDER BY periodo
                    """,
                "semana" => """
                    SELECT DATE_TRUNC('week', d.fecha_emision) AS periodo,
                           COUNT(DISTINCT d.id) AS total_dtes,
                           ROUND(SUM(d.total)::numeric, 2) AS total_monto
                    FROM dtes d
                    WHERE d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                    GROUP BY DATE_TRUNC('week', d.fecha_emision)
                    ORDER BY periodo
                    """,
                _ => """
                    SELECT DATE_TRUNC('month', d.fecha_emision) AS periodo,
                           COUNT(DISTINCT d.id) AS total_dtes,
                           ROUND(SUM(d.total)::numeric, 2) AS total_monto
                    FROM dtes d
                    WHERE d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                    GROUP BY DATE_TRUNC('month', d.fecha_emision)
                    ORDER BY periodo
                    """
            };

            var resultados = await db.Database.SqlQueryRaw<DtePeriodoRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record DtePeriodoRow(DateTime Periodo, long TotalDtes, decimal TotalMonto);

