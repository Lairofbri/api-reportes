using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class MontosFacturadosEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/montos-facturados", async (
            DteDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT DATE_TRUNC('month', d.fecha_emision) AS periodo,
                       d.tipo_dte,
                       COUNT(DISTINCT d.id) AS total_dtes,
                       ROUND(SUM(d.total)::numeric, 2) AS total_monto,
                       ROUND(SUM(d.subtotal)::numeric, 2) AS total_subtotal,
                       ROUND(SUM(d.iva)::numeric, 2) AS total_iva,
                       ROUND(SUM(d.total - COALESCE(d.iva, 0))::numeric, 2) AS total_exento
                FROM dtes d
                WHERE d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                  AND d.estado = 'aceptado'
                GROUP BY DATE_TRUNC('month', d.fecha_emision), d.tipo_dte
                ORDER BY periodo, d.tipo_dte
                """;

            var resultados = await db.Database.SqlQueryRaw<MontosFacturadosRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record MontosFacturadosRow(
    DateTime Periodo, string TipoDte,
    long TotalDtes, decimal TotalMonto,
    decimal TotalSubtotal, decimal TotalIva, decimal TotalExento);

