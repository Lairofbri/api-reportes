using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ordenes;

public static class HorasPicoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ordenes/horas-pico", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT EXTRACT(HOUR FROM o.creado_en)::int AS hora,
                       COUNT(DISTINCT o.id) AS total_ordenes,
                       ROUND(SUM(o.total)::numeric, 2) AS total_ingresos
                FROM ordenes o
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY EXTRACT(HOUR FROM o.creado_en)
                ORDER BY hora
                """;

            var resultados = await db.Database.SqlQueryRaw<HorasPicoRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record HorasPicoRow(int Hora, long TotalOrdenes, decimal TotalIngresos);

