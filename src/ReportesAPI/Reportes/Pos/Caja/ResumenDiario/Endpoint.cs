using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Caja;

public static class ResumenDiarioEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/caja/resumen-diario", async (
            PosDbContext db,
            DateTime? fecha) =>
        {
            var dia = fecha ?? DateTime.Today;

            var sql = """
                SELECT c.id AS caja_id,
                       c.fecha_apertura,
                       c.monto_inicial,
                       c.monto_final,
                       c.estado,
                       u.nombre AS usuario_nombre,
                       s.nombre AS sucursal_nombre,
                       v.total_ventas,
                       v.cantidad_ordenes,
                       v.ticket_promedio
                FROM cajas c
                LEFT JOIN usuarios u ON u.id = c.usuario_apertura_id
                LEFT JOIN sucursales s ON s.id = c.sucursal_id
                LEFT JOIN LATERAL (
                    SELECT COUNT(DISTINCT o.id) AS cantidad_ordenes,
                           ROUND(SUM(p.monto)::numeric, 2) AS total_ventas,
                           ROUND(AVG(p.monto)::numeric, 2) AS ticket_promedio
                    FROM ordenes o
                    JOIN pagos p ON p.orden_id = o.id
                    WHERE o.creado_en::date = c.fecha_apertura::date
                      AND o.estado = 'pagada'
                ) v ON true
                WHERE c.fecha_apertura::date = @dia
                ORDER BY c.fecha_apertura DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<ResumenCajaRow>(
                sql,
                new NpgsqlParameter("@dia", dia.Date)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, fecha = dia.Date });
        });
    }
}

public record ResumenCajaRow(
    Guid CajaId, DateTime FechaApertura,
    decimal MontoInicial, decimal? MontoFinal, string Estado,
    string? UsuarioNombre, string? SucursalNombre,
    decimal? TotalVentas, long? CantidadOrdenes, decimal? TicketPromedio);

