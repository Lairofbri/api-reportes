using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Costos;

public static class CostoPorProductoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/costos/por-producto", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT
                    p.id AS ProductoId,
                    p.nombre AS ProductoNombre,
                    p.costo_promedio AS CostoPromedio,
                    COALESCE(SUM(oi.cantidad), 0) AS TotalVendidos,
                    COALESCE(SUM(oi.cantidad * oi.precio_unitario), 0) AS TotalVentas,
                    ROUND(CAST(COALESCE(p.costo_promedio, 0) * COALESCE(SUM(oi.cantidad), 0) AS NUMERIC), 2) AS CostoTotal,
                    ROUND(CAST(
                        CASE WHEN COALESCE(SUM(oi.cantidad * oi.precio_unitario), 0) = 0 THEN 0
                        ELSE ((COALESCE(SUM(oi.cantidad * oi.precio_unitario), 0) - (COALESCE(p.costo_promedio, 0) * COALESCE(SUM(oi.cantidad), 0))) / COALESCE(SUM(oi.cantidad * oi.precio_unitario), 0)) * 100
                        END AS NUMERIC), 1) AS MargenPct
                FROM productos p
                LEFT JOIN orden_items oi ON oi.producto_id = p.id
                LEFT JOIN ordenes o ON o.id = oi.orden_id AND o.estado = 'pagada'
                    AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                WHERE p.tenant_id = @tenantId AND p.se_vende = true
                GROUP BY p.id, p.nombre, p.costo_promedio
                ORDER BY CostoTotal DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<CostoProductoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfCostoPorProducto(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfCostoPorProducto(List<CostoProductoRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var sumVentas = data.Sum(r => r.TotalVentas);
        var sumCosto = data.Sum(r => r.CostoTotal);
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Costo por Producto");
        pdf.Empresa(empresa)
           .Reporte("Costo por Producto")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.ProductoNombre ?? "N/A",
            r.TotalVendidos.ToString("N0"),
            r.CostoPromedio.ToString("N2"),
            r.TotalVentas.ToString("N2"),
            r.CostoTotal.ToString("N2"),
            r.MargenPct.ToString("N1") + "%"
        });

        pdf.Tabla(
            headers: ["Producto", "Vendidos", "Costo Unit.", "Ventas", "Costo Total", "Margen %"],
            rows: rows,
            totalRow: ["Total", "", "", sumVentas.ToString("N2"), sumCosto.ToString("N2"), ""]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"costo-por-producto-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record CostoProductoRow(Guid ProductoId, string ProductoNombre, decimal CostoPromedio, long TotalVendidos, decimal TotalVentas, decimal CostoTotal, decimal MargenPct);
