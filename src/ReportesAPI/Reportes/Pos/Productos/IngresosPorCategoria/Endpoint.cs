using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Productos;

public static class IngresosPorCategoriaEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/productos/ingresos-por-categoria", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT cat.id AS categoria_id,
                       cat.nombre AS categoria_nombre,
                       COUNT(DISTINCT oi.id) AS total_items,
                       SUM(oi.cantidad * oi.precio_unitario) AS total_ingresos
                FROM orden_items oi
                JOIN ordenes o ON o.id = oi.orden_id
                LEFT JOIN productos p ON p.id = oi.producto_id
                LEFT JOIN categorias cat ON cat.id = p.categoria_id
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                  AND oi.producto_id IS NOT NULL
                GROUP BY cat.id, cat.nombre
                ORDER BY total_ingresos DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<IngresosCategoriaRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record IngresosCategoriaRow(Guid? CategoriaId, string? CategoriaNombre, long TotalItems, decimal TotalIngresos);

