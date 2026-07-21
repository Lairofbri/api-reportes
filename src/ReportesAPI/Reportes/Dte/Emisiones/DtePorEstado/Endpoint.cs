using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class DtePorEstadoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/por-estado", async (
            DteDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT d.estado,
                       COUNT(DISTINCT d.id) AS total_dtes,
                       ROUND(SUM(d.total)::numeric, 2) AS total_monto
                FROM dtes d
                WHERE d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                GROUP BY d.estado
                ORDER BY total_dtes DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DteEstadoRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record DteEstadoRow(string Estado, long TotalDtes, decimal TotalMonto);

