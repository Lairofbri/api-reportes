using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Consolidados;

public static class IngresosVsDteEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/consolidados/ingresos-vs-dte", async (
            PosDbContext posDb,
            DteDbContext dteDb,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sqlPos = """
                SELECT DATE_TRUNC('month', o.creado_en) AS mes,
                       ROUND(SUM(o.total)::numeric, 2) AS ingresos_pos
                FROM ordenes o
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY DATE_TRUNC('month', o.creado_en)
                ORDER BY mes
                """;

            var ingresosPos = await posDb.Database.SqlQueryRaw<IngresosMesRow>(
                sqlPos,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            var sqlDte = """
                SELECT DATE_TRUNC('month', d.fecha_emision) AS mes,
                       ROUND(SUM(d.total)::numeric, 2) AS total_dte
                FROM dtes d
                WHERE d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                  AND d.estado = 'aceptado'
                GROUP BY DATE_TRUNC('month', d.fecha_emision)
                ORDER BY mes
                """;

            var dtesAceptados = await dteDb.Database.SqlQueryRaw<IngresosMesRow>(
                sqlDte,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            var consolidado = ingresosPos
                .GroupJoin(dtesAceptados, p => p.Mes, d => d.Mes, (p, d) => new
                {
                    mes = p.Mes,
                    ingresos_pos = p.IngresosPos,
                    total_dte = d.FirstOrDefault()?.IngresosPos ?? 0,
                    diferencia = p.IngresosPos - (d.FirstOrDefault()?.IngresosPos ?? 0)
                })
                .OrderBy(r => r.mes)
                .ToList();

            return Results.Ok(new { datos = consolidado, desde = @de, hasta = @ha });
        });
    }
}

public record IngresosMesRow(DateTime Mes, decimal IngresosPos);

