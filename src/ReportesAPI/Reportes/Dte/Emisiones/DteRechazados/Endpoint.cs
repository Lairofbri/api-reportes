using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class DteRechazadosEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/rechazados", async (
            DteDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT d.id AS dte_id,
                       d.codigo_generacion,
                       d.tipo_dte,
                       d.fecha_emision,
                       d.total,
                       d.observaciones,
                       d.estado
                FROM dtes d
                WHERE d.estado = 'rechazado'
                  AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                ORDER BY d.fecha_emision DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DteRechazadoRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record DteRechazadoRow(Guid DteId, string CodigoGeneracion, string TipoDte, DateTime FechaEmision, decimal Total, string? Observaciones, string Estado);

