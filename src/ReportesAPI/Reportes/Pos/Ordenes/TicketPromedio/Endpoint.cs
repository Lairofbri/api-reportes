using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ordenes;

public static class TicketPromedioEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ordenes/ticket-promedio", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT o.creado_en::date AS fecha,
                       COUNT(DISTINCT o.id) AS total_ordenes,
                       ROUND(AVG(o.total)::numeric, 2) AS ticket_promedio,
                       ROUND(PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY o.total)::numeric, 2) AS mediana,
                       MIN(o.total) AS ticket_minimo,
                       MAX(o.total) AS ticket_maximo
                FROM ordenes o
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY o.creado_en::date
                ORDER BY fecha
                """;

            var resultados = await db.Database.SqlQueryRaw<TicketPromedioRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record TicketPromedioRow(DateOnly Fecha, long TotalOrdenes, decimal TicketPromedio, decimal Mediana, decimal TicketMinimo, decimal TicketMaximo);

