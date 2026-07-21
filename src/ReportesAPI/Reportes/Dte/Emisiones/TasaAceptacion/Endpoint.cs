using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class TasaAceptacionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/tasa-aceptacion", async (
            DteDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT DATE_TRUNC('month', d.fecha_emision) AS periodo,
                       COUNT(DISTINCT d.id) FILTER (WHERE d.estado = 'aceptado') AS aceptados,
                       COUNT(DISTINCT d.id) FILTER (WHERE d.estado = 'rechazado') AS rechazados,
                       COUNT(DISTINCT d.id) FILTER (WHERE d.estado = 'contingencia') AS contingencias,
                       COUNT(DISTINCT d.id) AS total,
                       ROUND(
                           (COUNT(DISTINCT d.id) FILTER (WHERE d.estado = 'aceptado') * 100.0 /
                           NULLIF(COUNT(DISTINCT d.id), 0))::numeric, 2
                       ) AS porcentaje_aceptacion
                FROM dtes d
                WHERE d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                GROUP BY DATE_TRUNC('month', d.fecha_emision)
                ORDER BY periodo
                """;

            var resultados = await db.Database.SqlQueryRaw<TasaAceptacionRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record TasaAceptacionRow(DateTime Periodo, long Aceptados, long Rechazados, long Contingencias, long Total, decimal PorcentajeAceptacion);

