using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Contingencia;

public static class EventosContingenciaEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/contingencia/eventos", async (
            DteDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT c.id AS ContingenciaId,
                       c.fecha_inicio,
                       c.fecha_fin,
                       c.motivo_contingencia AS Motivo,
                       c.estado,
                       d.cnt AS DteAfectados
                FROM contingencias c
                LEFT JOIN LATERAL (
                    SELECT COUNT(*) AS cnt
                    FROM dtes d2
                    WHERE d2.tenant_id = c.tenant_id
                      AND d2.es_contingencia = true
                      AND d2.fecha_emision >= c.fecha_inicio
                      AND (c.fecha_fin IS NULL OR d2.fecha_emision <= c.fecha_fin)
                ) d ON true
                WHERE c.tenant_id = @tenantId
                  AND c.fecha_inicio >= @de AND c.fecha_inicio < @ha::date + 1
                GROUP BY c.id, c.fecha_inicio, c.fecha_fin, c.motivo_contingencia, c.estado
                ORDER BY c.fecha_inicio DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<EventoContingenciaRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf") return await PdfEventosContingencia(db, tenantContext.TenantId, resultados, @de, @ha);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfEventosContingencia(DteDbContext db, Guid? tenantId, List<EventoContingenciaRow> data, DateTime desde, DateTime hasta)
    {
        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("Eventos de Contingencia");
        pdf.Empresa(empresa)
           .Reporte("Eventos de Contingencia")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.FechaInicio.ToString("dd/MM/yyyy"),
            "",
            r.Motivo ?? "",
            r.DteAfectados.ToString("N0"),
            r.Estado
        });

        pdf.Tabla(
            headers: ["Fecha", "Tipo", "Motivo", "DTEs Afectados", "Estado"],
            rows: rows
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"eventos-contingencia-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record EventoContingenciaRow(Guid ContingenciaId, DateTime FechaInicio, DateTime? FechaFin, string? Motivo, string Estado, long DteAfectados);
