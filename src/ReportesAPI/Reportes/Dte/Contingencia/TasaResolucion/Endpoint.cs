using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Contingencia;

public static class TasaResolucionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/contingencia/tasa-resolucion", async (
            DteDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-3);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT DATE_TRUNC('month', c.fecha_inicio) AS periodo,
                       COUNT(DISTINCT c.id) AS total_eventos,
                       COUNT(DISTINCT c.id) FILTER (WHERE c.estado = 'resuelto') AS resueltos,
                       COUNT(DISTINCT c.id) FILTER (WHERE c.estado = 'pendiente') AS pendientes,
                       ROUND(
                           (EXTRACT(EPOCH FROM AVG(c.fecha_fin - c.fecha_inicio)) / 3600)::numeric, 2
                       ) AS horas_promedio_resolucion
                FROM contingencias c
                WHERE c.fecha_inicio >= @de AND c.fecha_inicio < @ha::date + 1
                GROUP BY DATE_TRUNC('month', c.fecha_inicio)
                ORDER BY periodo
                """;

            var resultados = await db.Database.SqlQueryRaw<TasaResolucionRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record TasaResolucionRow(DateTime Periodo, long TotalEventos, long Resueltos, long Pendientes, decimal? HorasPromedioResolucion);

