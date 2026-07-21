using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ventas;

public static class VentasPorMetodoPagoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ventas/por-metodo-pago", async (
            PosDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT p.metodo,
                       c.nombre AS metodo_nombre,
                       COUNT(DISTINCT p.orden_id) AS total_ordenes,
                       SUM(p.monto) AS total_monto
                FROM pagos p
                LEFT JOIN catalogos c ON c.grupo = 'metodos_pago' AND c.codigo = p.metodo
                WHERE p.creado_en >= @de AND p.creado_en < @ha::date + 1
                GROUP BY p.metodo, c.nombre
                ORDER BY total_monto DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<VentasMetodoRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record VentasMetodoRow(string Metodo, string? MetodoNombre, long TotalOrdenes, decimal TotalMonto);

