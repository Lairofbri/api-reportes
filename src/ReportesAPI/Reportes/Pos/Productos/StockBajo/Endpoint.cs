using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Productos;

public static class StockBajoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/productos/stock-bajo", async (
            PosDbContext db,
            int? umbral) =>
        {
            var minStock = umbral ?? 10;

            var sql = """
                SELECT p.id AS producto_id,
                       p.nombre AS producto_nombre,
                       cat.nombre AS categoria_nombre,
                       p.stock
                FROM productos p
                LEFT JOIN categorias cat ON cat.id = p.categoria_id
                WHERE p.activo = true AND p.stock <= @minStock
                ORDER BY p.stock ASC
                """;

            var resultados = await db.Database.SqlQueryRaw<StockBajoRow>(
                sql,
                new NpgsqlParameter("@minStock", minStock)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, umbral = minStock });
        });
    }
}

public record StockBajoRow(Guid ProductoId, string ProductoNombre, string? CategoriaNombre, int Stock);

