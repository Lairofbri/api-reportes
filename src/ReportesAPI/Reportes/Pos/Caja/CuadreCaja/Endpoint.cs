using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Caja;

public static class CuadreCajaEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/caja/cuadre", async (
            PosDbContext db,
            Guid? cajaId) =>
        {
            if (cajaId is null)
                return Results.BadRequest(new { error = "cajaId es requerido" });

            var sql = """
                SELECT c.id AS caja_id,
                       c.fecha_apertura,
                       c.monto_inicial,
                       c.monto_final AS monto_cerrado,
                       COALESCE(v.total_ventas, 0) AS ventas_totales,
                       COALESCE(m.total_ingresos, 0) AS ingresos_manuales,
                       COALESCE(m.total_retiros, 0) AS retiros_manuales,
                       ROUND((c.monto_inicial + COALESCE(v.total_ventas, 0) + COALESCE(m.total_ingresos, 0) - COALESCE(m.total_retiros, 0))::numeric, 2) AS monto_esperado,
                       CASE
                           WHEN c.monto_final IS NOT NULL
                           THEN ROUND((c.monto_final - (c.monto_inicial + COALESCE(v.total_ventas, 0) + COALESCE(m.total_ingresos, 0) - COALESCE(m.total_retiros, 0)))::numeric, 2)
                           ELSE NULL
                       END AS diferencia
                FROM cajas c
                LEFT JOIN LATERAL (
                    SELECT ROUND(SUM(p.monto)::numeric, 2) AS total_ventas
                    FROM ordenes o
                    JOIN pagos p ON p.orden_id = o.id
                    WHERE o.creado_en >= c.fecha_apertura AND (o.creado_en <= c.fecha_cierre OR c.fecha_cierre IS NULL)
                      AND o.estado = 'pagada'
                ) v ON true
                LEFT JOIN LATERAL (
                    SELECT ROUND(SUM(mc.monto) FILTER (WHERE mc.tipo = 'ingreso')::numeric, 2) AS total_ingresos,
                           ROUND(SUM(mc.monto) FILTER (WHERE mc.tipo = 'retiro')::numeric, 2) AS total_retiros
                    FROM movimientos_caja mc
                    WHERE mc.caja_id = c.id
                ) m ON true
                WHERE c.id = @cajaId
                """;

            var resultado = await db.Database.SqlQueryRaw<CuadreCajaRow>(
                sql,
                new NpgsqlParameter("@cajaId", cajaId.Value)
            ).FirstOrDefaultAsync();

            if (resultado is null)
                return Results.NotFound(new { error = "Caja no encontrada" });

            return Results.Ok(resultado);
        });
    }
}

public record CuadreCajaRow(
    Guid CajaId, DateTime FechaApertura,
    decimal MontoInicial, decimal? MontoCerrado,
    decimal VentasTotales, decimal IngresosManuales, decimal RetirosManuales,
    decimal MontoEsperado, decimal? Diferencia);

