using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ventas;

public static class VentasPorOrigenEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ventas/por-origen", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT o.origen,
                       c.nombre AS origen_nombre,
                       COUNT(DISTINCT o.id) AS total_ordenes,
                       SUM(o.total) AS total_ingresos
                FROM ordenes o
                LEFT JOIN catalogos c ON c.grupo = 'origenes_orden' AND c.codigo = o.origen
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY o.origen, c.nombre
                ORDER BY total_ingresos DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<VentasOrigenRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record VentasOrigenRow(string Origen, string? OrigenNombre, long TotalOrdenes, decimal TotalIngresos);

