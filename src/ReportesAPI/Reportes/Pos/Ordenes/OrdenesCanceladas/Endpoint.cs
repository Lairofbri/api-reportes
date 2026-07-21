using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ordenes;

public static class OrdenesCanceladasEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ordenes/canceladas", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT o.creado_en::date AS fecha,
                       COUNT(DISTINCT o.id) FILTER (WHERE o.estado = 'cancelada') AS canceladas,
                       COUNT(DISTINCT o.id) FILTER (WHERE o.estado = 'pagada') AS completadas,
                       COUNT(DISTINCT o.id) AS total
                FROM ordenes o
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                GROUP BY o.creado_en::date
                ORDER BY fecha
                """;

            var resultados = await db.Database.SqlQueryRaw<OrdenCanceladaRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record OrdenCanceladaRow(DateOnly Fecha, long Canceladas, long Completadas, long Total);

