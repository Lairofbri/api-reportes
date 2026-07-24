using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ordenes;

public static class OrdenesCanceladasEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ordenes/canceladas", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT o.creado_en::date AS fecha,
                       COUNT(DISTINCT o.id) FILTER (WHERE o.estado = 'cancelada') AS canceladas,
                       COUNT(DISTINCT o.id) FILTER (WHERE o.estado = 'pagada') AS completadas,
                       COUNT(DISTINCT o.id) AS total
                FROM ordenes o
                WHERE o.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                GROUP BY o.creado_en::date
                ORDER BY Fecha
                """;

            var resultados = await db.Database.SqlQueryRaw<OrdenCanceladaRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfOrdenesCanceladas(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfOrdenesCanceladas(List<OrdenCanceladaRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var totalCanceladas = data.Sum(r => r.Canceladas);
        var totalCompletadas = data.Sum(r => r.Completadas);
        var totalGeneral = data.Sum(r => r.Total);
        var porcCancelacion = totalGeneral > 0 ? (double)totalCanceladas / totalGeneral * 100 : 0;
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Ordenes Canceladas");
        pdf.Empresa(empresa)
           .Reporte("Ordenes Canceladas")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.Fecha.ToString("dd/MM/yyyy"),
            r.Canceladas.ToString("N0"),
            r.Completadas.ToString("N0"),
            (r.Total > 0 ? (double)r.Canceladas / r.Total * 100 : 0).ToString("F2") + " %"
        });

        pdf.Tabla(
            headers: ["Periodo", "Canceladas", "Completadas", "% Cancelacion"],
            rows: rows,
            totalRow: ["Total", totalCanceladas.ToString("N0"), totalCompletadas.ToString("N0"), porcCancelacion.ToString("F2") + " %"]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"ordenes-canceladas-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record OrdenCanceladaRow(DateOnly Fecha, long Canceladas, long Completadas, long Total);

