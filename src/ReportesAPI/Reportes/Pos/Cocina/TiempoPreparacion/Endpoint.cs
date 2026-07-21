using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Cocina;

public static class TiempoPreparacionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/cocina/tiempo-preparacion", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT oi.producto_id,
                       p.nombre AS producto_nombre,
                       cat.nombre AS categoria_nombre,
                       COUNT(oi.id) AS total_items,
                       ROUND(AVG(EXTRACT(EPOCH FROM (COALESCE(oi.actualizado_en, oi.creado_en) - oi.creado_en)) / 60)::numeric, 2) AS minutos_promedio
                FROM orden_items oi
                JOIN ordenes o ON o.id = oi.orden_id
                LEFT JOIN productos p ON p.id = oi.producto_id
                LEFT JOIN categorias cat ON cat.id = p.categoria_id
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND oi.estado IN ('listo', 'cancelado')
                  AND oi.producto_id IS NOT NULL
                GROUP BY oi.producto_id, p.nombre, cat.nombre
                ORDER BY minutos_promedio DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<TiempoPrepRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record TiempoPrepRow(Guid? ProductoId, string? ProductoNombre, string? CategoriaNombre, long TotalItems, decimal MinutosPromedio);

