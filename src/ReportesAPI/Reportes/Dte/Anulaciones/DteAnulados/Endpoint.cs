using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Anulaciones;

public static class DteAnuladosEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/anulaciones", async (
            DteDbContext db,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-3);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT d.id AS dte_id,
                       d.codigo_generacion,
                       d.tipo_dte,
                       d.fecha_emision,
                       d.total,
                       d.fecha_anulacion,
                       d.motivo_anulacion
                FROM dtes d
                WHERE d.estado = 'anulado'
                  AND d.fecha_anulacion >= @de AND d.fecha_anulacion < @ha::date + 1
                ORDER BY d.fecha_anulacion DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DteAnuladoRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record DteAnuladoRow(Guid DteId, string CodigoGeneracion, string TipoDte, DateTime FechaEmision, decimal Total, DateTime? FechaAnulacion, string? MotivoAnulacion);

