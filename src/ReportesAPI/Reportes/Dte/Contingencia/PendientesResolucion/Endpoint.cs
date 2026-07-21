using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Contingencia;

public static class PendientesResolucionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/contingencia/pendientes", async (DteDbContext db) =>
        {
            var sql = """
                SELECT d.id AS dte_id,
                       d.codigo_generacion,
                       d.tipo_dte,
                       d.fecha_emision,
                       d.total,
                       c.fecha_inicio AS contingencia_desde,
                       c.motivo
                FROM dtes d
                JOIN contingencias c ON c.id = d.contingencia_id
                WHERE d.estado = 'contingencia'
                  AND c.estado = 'pendiente'
                ORDER BY d.fecha_emision ASC
                """;

            var resultados = await db.Database.SqlQueryRaw<PendienteResolucionRow>(sql).ToListAsync();

            return Results.Ok(new { datos = resultados, total_pendientes = resultados.Count });
        });
    }
}

public record PendienteResolucionRow(Guid DteId, string CodigoGeneracion, string TipoDte, DateTime FechaEmision, decimal Total, DateTime ContingenciaDesde, string? Motivo);

