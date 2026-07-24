using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Sucursales;

public static class ComparativaSucursalesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/sucursales/comparativa", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT s.id AS SucursalId,
                       S.nombre AS SucursalNombre,
                       COUNT(DISTINCT o.id) AS TotalOrdenes,
                       ROUND(SUM(o.total)::numeric, 2) AS TotalIngresos,
                       ROUND(AVG(o.total)::numeric, 2) AS TicketPromedio,
                       COUNT(DISTINCT o.usuario_id) AS MeserosActivos
                FROM ordenes o
                JOIN sucursales s ON s.id = o.sucursal_id
                WHERE o.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY s.id, S.nombre
                ORDER BY TotalIngresos DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<ComparativaSucursalRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfComparativaSucursales(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfComparativaSucursales(List<ComparativaSucursalRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var totalOrdenes = data.Sum(r => r.TotalOrdenes);
        var totalIngresos = data.Sum(r => r.TotalIngresos);
        var ticketPromedio = totalOrdenes > 0 ? totalIngresos / totalOrdenes : 0;
        var totalMeseros = data.Sum(r => r.MeserosActivos);
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Comparativa de Sucursales");
        pdf.Empresa(empresa)
           .Reporte("Comparativa de Sucursales")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.SucursalNombre,
            r.TotalOrdenes.ToString("N0"),
            r.TotalIngresos.ToString("N2"),
            r.TicketPromedio.ToString("N2"),
            r.MeserosActivos.ToString("N0")
        });

        pdf.Tabla(
            headers: ["Sucursal", "Ordenes", "Ingresos", "Ticket Promedio", "Meseros"],
            rows: rows,
            totalRow: ["Total", totalOrdenes.ToString("N0"), totalIngresos.ToString("N2"), ticketPromedio.ToString("N2"), totalMeseros.ToString("N0")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"comparativa-sucursales-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record ComparativaSucursalRow(Guid SucursalId, string SucursalNombre, long TotalOrdenes, decimal TotalIngresos, decimal TicketPromedio, long MeserosActivos);

