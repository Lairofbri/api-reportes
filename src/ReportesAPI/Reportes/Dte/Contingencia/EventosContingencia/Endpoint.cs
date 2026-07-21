using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Contingencia;

public static class EventosContingenciaEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/contingencia/eventos", async (
            DteDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT c.id AS contingencia_id,
                       c.fecha_inicio,
                       c.fecha_fin,
                       c.motivo,
                       c.estado,
                       COUNT(d.id) AS dte_afectados
                FROM contingencias c
                LEFT JOIN dtes d ON d.contingencia_id = c.id
                WHERE c.fecha_inicio >= @de AND c.fecha_inicio < @ha::date + 1
                GROUP BY c.id, c.fecha_inicio, c.fecha_fin, c.motivo, c.estado
                ORDER BY c.fecha_inicio DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<EventoContingenciaRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record EventoContingenciaRow(Guid ContingenciaId, DateTime FechaInicio, DateTime? FechaFin, string? Motivo, string Estado, long DteAfectados);

