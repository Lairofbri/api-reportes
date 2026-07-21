using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class DtePorTipoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/por-tipo", async (
            DteDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT d.tipo_dte,
                       COUNT(DISTINCT d.id) AS total_dtes,
                       ROUND(SUM(d.total)::numeric, 2) AS total_monto,
                       ROUND(AVG(d.total)::numeric, 2) AS promedio_monto,
                       ROUND(SUM(d.iva)::numeric, 2) AS total_iva
                FROM dtes d
                WHERE d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                GROUP BY d.tipo_dte
                ORDER BY total_dtes DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DteTipoRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record DteTipoRow(string TipoDte, long TotalDtes, decimal TotalMonto, decimal PromedioMonto, decimal TotalIva);

