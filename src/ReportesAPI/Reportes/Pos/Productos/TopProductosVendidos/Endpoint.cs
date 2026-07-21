using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Productos;

public static class TopProductosVendidosEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/productos/top", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta,
            int? limite) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;
            var top = limite ?? 20;

            var sql = """
                SELECT oi.producto_id,
                       p.nombre AS producto_nombre,
                       cat.nombre AS categoria_nombre,
                       SUM(oi.cantidad) AS total_vendidos,
                       SUM(oi.cantidad * oi.precio_unitario) AS total_ingresos
                FROM orden_items oi
                JOIN ordenes o ON o.id = oi.orden_id
                LEFT JOIN productos p ON p.id = oi.producto_id
                LEFT JOIN categorias cat ON cat.id = p.categoria_id
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                  AND oi.producto_id IS NOT NULL
                GROUP BY oi.producto_id, p.nombre, cat.nombre
                ORDER BY total_vendidos DESC
                LIMIT @top
                """;

            var resultados = await db.Database.SqlQueryRaw<TopProductoRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha),
                new NpgsqlParameter("@top", top)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record TopProductoRow(Guid? ProductoId, string? ProductoNombre, string? CategoriaNombre, long TotalVendidos, decimal TotalIngresos);

