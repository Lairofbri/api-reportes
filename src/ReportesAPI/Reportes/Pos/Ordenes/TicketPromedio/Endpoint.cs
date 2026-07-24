using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ordenes;

public static class TicketPromedioEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ordenes/ticket-promedio", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT o.creado_en::date AS Fecha,
                       COUNT(DISTINCT o.id) AS TotalOrdenes,
                       ROUND(AVG(o.total)::numeric, 2) AS TicketPromedio,
                       ROUND(PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY O.total)::numeric, 2) AS Mediana,
                       MIN(o.total) AS TicketMinimo,
                       MAX(o.total) AS TicketMaximo
                FROM ordenes o
                WHERE o.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY o.creado_en::date
                ORDER BY Fecha
                """;

            var resultados = await db.Database.SqlQueryRaw<TicketPromedioRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfTicketPromedio(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfTicketPromedio(List<TicketPromedioRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var totalOrdenes = data.Sum(r => r.TotalOrdenes);
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Ticket Promedio");
        pdf.Empresa(empresa)
           .Reporte("Ticket Promedio")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.Fecha.ToString("dd/MM/yyyy"),
            r.TotalOrdenes.ToString("N0"),
            r.TicketPromedio.ToString("N2"),
            r.Mediana.ToString("N2"),
            r.TicketMinimo.ToString("N2"),
            r.TicketMaximo.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Fecha", "Ordenes", "Promedio", "Mediana", "Minimo", "Maximo"],
            rows: rows,
            totalRow: ["Total", totalOrdenes.ToString("N0"), "", "", "", ""]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"ticket-promedio-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record TicketPromedioRow(DateOnly Fecha, long TotalOrdenes, decimal TicketPromedio, decimal Mediana, decimal TicketMinimo, decimal TicketMaximo);

