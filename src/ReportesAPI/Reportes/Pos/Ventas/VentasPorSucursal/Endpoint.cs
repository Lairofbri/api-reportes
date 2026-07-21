using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ventas;

public static class VentasPorSucursalEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ventas/por-sucursal", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT o.sucursal_id,
                       s.nombre AS sucursal_nombre,
                       COUNT(DISTINCT o.id) AS total_ordenes,
                       SUM(o.total) AS total_ingresos,
                       AVG(o.total) AS ticket_promedio
                FROM ordenes o
                LEFT JOIN sucursales s ON s.id = o.sucursal_id
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY o.sucursal_id, s.nombre
                ORDER BY total_ingresos DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<VentasSucursalRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record VentasSucursalRow(Guid SucursalId, string? SucursalNombre, long TotalOrdenes, decimal TotalIngresos, decimal TicketPromedio);

