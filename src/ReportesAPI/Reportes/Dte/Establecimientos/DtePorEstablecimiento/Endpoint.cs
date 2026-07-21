using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Establecimientos;

public static class DtePorEstablecimientoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/establecimientos/por-establecimiento", async (
            DteDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT e.id AS establecimiento_id,
                       e.nombre,
                       e.cod_estable_mh,
                       e.cod_punto_venta_mh,
                       COUNT(DISTINCT d.id) AS total_dtes,
                       ROUND(SUM(d.total)::numeric, 2) AS total_monto
                FROM dtes d
                JOIN establecimientos e ON e.id = d.establecimiento_id
                WHERE d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                GROUP BY e.id, e.nombre, e.cod_estable_mh, e.cod_punto_venta_mh
                ORDER BY total_dtes DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DteEstablecimientoRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record DteEstablecimientoRow(Guid EstablecimientoId, string Nombre, string CodEstableMH, string CodPuntoVentaMH, long TotalDtes, decimal TotalMonto);

