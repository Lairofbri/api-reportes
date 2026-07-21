using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Staff;

public static class DesempenoMeseroEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/staff/desempeno-mesero", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT o.usuario_id,
                       u.nombre AS mesero_nombre,
                       COUNT(DISTINCT o.id) AS total_ordenes,
                       ROUND(SUM(o.total)::numeric, 2) AS total_ventas,
                       ROUND(AVG(o.total)::numeric, 2) AS ticket_promedio,
                       ROUND(SUM(o.propina)::numeric, 2) AS total_propinas
                FROM ordenes o
                LEFT JOIN usuarios u ON u.id = o.usuario_id
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY o.usuario_id, u.nombre
                ORDER BY total_ventas DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DesempenoMeseroRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record DesempenoMeseroRow(Guid UsuarioId, string? MeseroNombre, long TotalOrdenes, decimal TotalVentas, decimal TicketPromedio, decimal TotalPropinas);

