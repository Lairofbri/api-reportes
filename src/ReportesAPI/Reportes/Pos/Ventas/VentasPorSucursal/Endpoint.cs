using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ventas;

public static class VentasPorSucursalEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ventas/por-sucursal", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT o.sucursal_id AS SucursalId,
                       S.nombre AS SucursalNombre,
                       COUNT(DISTINCT o.id) AS TotalOrdenes,
                       SUM(o.total) AS TotalIngresos,
                       AVG(o.total) AS TicketPromedio
                FROM ordenes o
                LEFT JOIN sucursales s ON s.id = o.sucursal_id
                WHERE o.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY o.sucursal_id, S.nombre
                ORDER BY TotalIngresos DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<VentasSucursalRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfVentasPorSucursal(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfVentasPorSucursal(List<VentasSucursalRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var sumTotalOrdenes = data.Sum(r => r.TotalOrdenes);
        var sumIngresos = data.Sum(r => r.TotalIngresos);
        var avgTicket = data.Count > 0 ? data.Average(r => r.TicketPromedio) : 0;
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Ventas por Sucursal");
        pdf.Empresa(empresa)
           .Reporte("Ventas por Sucursal")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.SucursalNombre ?? "Sin nombre",
            r.TotalOrdenes.ToString("N0"),
            r.TotalIngresos.ToString("N2"),
            r.TicketPromedio.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Sucursal", "Ordenes", "Ingresos", "Ticket Promedio"],
            rows: rows,
            totalRow: ["Total", sumTotalOrdenes.ToString("N0"), sumIngresos.ToString("N2"), avgTicket.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"ventas-por-sucursal-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record VentasSucursalRow(Guid SucursalId, string? SucursalNombre, long TotalOrdenes, decimal TotalIngresos, decimal TicketPromedio);

