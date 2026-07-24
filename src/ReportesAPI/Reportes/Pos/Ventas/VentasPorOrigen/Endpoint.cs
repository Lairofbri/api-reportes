using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ventas;

public static class VentasPorOrigenEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ventas/por-origen", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT o.origen,
                       c.label AS OrigenNombre,
                        COUNT(DISTINCT o.id) AS TotalOrdenes,
                        SUM(o.total) AS TotalIngresos
                FROM ordenes o
                LEFT JOIN catalogos c ON c.grupo = 'origenes_orden' AND c.valor = o.origen
                WHERE o.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY o.origen, c.label
                ORDER BY TotalIngresos DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<VentasOrigenRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfVentasPorOrigen(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfVentasPorOrigen(List<VentasOrigenRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var sumOrdenes = data.Sum(r => r.TotalOrdenes);
        var sumIngresos = data.Sum(r => r.TotalIngresos);
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Ventas por Origen");
        pdf.Empresa(empresa)
           .Reporte("Ventas por Origen")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.OrigenNombre ?? r.Origen ?? "Sin origen",
            r.TotalOrdenes.ToString("N0"),
            r.TotalIngresos.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Origen", "Ordenes", "Ingresos"],
            rows: rows,
            totalRow: ["Total", sumOrdenes.ToString("N0"), sumIngresos.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"ventas-por-origen-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record VentasOrigenRow(string Origen, string? OrigenNombre, long TotalOrdenes, decimal TotalIngresos);

