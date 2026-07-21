using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ventas;

public static class VentasPorTipoOrdenEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ventas/por-tipo-orden", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT o.tipo,
                       COUNT(DISTINCT o.id) AS total_ordenes,
                       SUM(o.total) AS total_ingresos,
                       AVG(o.total) AS ticket_promedio
                FROM ordenes o
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY o.tipo
                ORDER BY total_ingresos DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<VentasTipoOrdenRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record VentasTipoOrdenRow(string Tipo, long TotalOrdenes, decimal TotalIngresos, decimal TicketPromedio);

