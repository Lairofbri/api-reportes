using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Auditoria;

public static class LogOperacionesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/auditoria/operaciones", async (
            DteDbContext db,
            DateTime? desde,
            DateTime? hasta,
            int? limite) =>
        {
            var @de = desde ?? DateTime.Today.AddDays(-7);
            var @ha = hasta ?? DateTime.Today;
            var top = limite ?? 100;

            var sql = """
                SELECT a.id,
                       a.fecha,
                       a.usuario_id,
                       u.nombre AS usuario_nombre,
                       a.accion,
                       a.tabla_afectada,
                       a.registro_id,
                       a.detalle
                FROM auditoria a
                LEFT JOIN usuarios u ON u.id = a.usuario_id
                WHERE a.fecha >= @de AND a.fecha < @ha::date + 1
                ORDER BY a.fecha DESC
                LIMIT @top
                """;

            var resultados = await db.Database.SqlQueryRaw<LogOperacionRow>(
                sql,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha),
                new NpgsqlParameter("@top", top)
            ).ToListAsync();

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }
}

public record LogOperacionRow(Guid Id, DateTime Fecha, Guid? UsuarioId, string? UsuarioNombre, string Accion, string? TablaAfectada, Guid? RegistroId, string? Detalle);

