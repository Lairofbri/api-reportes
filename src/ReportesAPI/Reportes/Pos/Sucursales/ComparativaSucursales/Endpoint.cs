using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Sucursales;

public static class ComparativaSucursalesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/sucursales/comparativa", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT s.id AS sucursal_id,
                       s.nombre AS sucursal_nombre,
                       COUNT(DISTINCT o.id) AS total_ordenes,
                       ROUND(SUM(o.total)::numeric, 2) AS total_ingresos,
                       ROUND(AVG(o.total)::numeric, 2) AS ticket_promedio,
                       COUNT(DISTINCT o.usuario_id) AS meseros_activos
                FROM ordenes o
                JOIN sucursales s ON s.id = o.sucursal_id
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY s.id, s.nombre
                ORDER BY total_ingresos DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<ComparativaSucursalRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record ComparativaSucursalRow(Guid SucursalId, string SucursalNombre, long TotalOrdenes, decimal TotalIngresos, decimal TicketPromedio, long MeserosActivos);

