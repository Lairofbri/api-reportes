using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Contingencia;

public static class TasaResolucionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/contingencia/tasa-resolucion", async (
            DteDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-3);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT DATE_TRUNC('month', c.fecha_inicio) AS Periodo,
                       COUNT(DISTINCT c.id) AS TotalEventos,
                       COUNT(DISTINCT c.id) FILTER (WHERE c.estado = 'resuelto') AS Resueltos,
                       COUNT(DISTINCT c.id) FILTER (WHERE c.estado = 'pendiente') AS Pendientes,
                       ROUND(
                           (EXTRACT(EPOCH FROM AVG(c.fecha_fin - c.fecha_inicio)) / 3600)::numeric, 2
                       ) AS HorasPromedioResolucion
                FROM contingencias c
                WHERE c.tenant_id = @tenantId
                  AND c.fecha_inicio >= @de AND c.fecha_inicio < @ha::date + 1
                GROUP BY DATE_TRUNC('month', c.fecha_inicio)
                ORDER BY Periodo
                """;

            var resultados = await db.Database.SqlQueryRaw<TasaResolucionRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf") return await PdfTasaResolucion(db, tenantContext.TenantId, resultados, @de, @ha);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfTasaResolucion(DteDbContext db, Guid? tenantId, List<TasaResolucionRow> data, DateTime desde, DateTime hasta)
    {
        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("Tasa de Resolucion");
        pdf.Empresa(empresa)
           .Reporte("Tasa de Resolucion")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.Periodo.ToString("MM/yyyy"),
            r.Resueltos.ToString("N0"),
            r.Pendientes.ToString("N0"),
            r.TotalEventos > 0
                ? (Math.Round((double)r.Resueltos / r.TotalEventos * 100, 2).ToString("N2") + "%")
                : "0%"
        });

        pdf.Tabla(
            headers: ["Periodo", "Resueltos", "Pendientes", "Tasa Resolucion"],
            rows: rows
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"tasa-resolucion-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record TasaResolucionRow(DateTime Periodo, long TotalEventos, long Resueltos, long Pendientes, decimal? HorasPromedioResolucion);
