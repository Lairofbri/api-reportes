using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Costos;

public static class CostoPorCategoriaEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/costos/por-categoria", async (
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
                    COALESCE(c.nombre, 'Sin categoría') AS CategoriaNombre,
                    COUNT(DISTINCT p.id) AS Productos,
                    COALESCE(SUM(oi.cantidad), 0) AS TotalVendidos,
                    COALESCE(SUM(oi.cantidad * oi.precio_unitario), 0) AS TotalVentas,
                    ROUND(CAST(COALESCE(p.costo_promedio, 0) * COALESCE(SUM(oi.cantidad), 0) AS NUMERIC), 2) AS CostoTotal
                FROM productos p
                LEFT JOIN categorias c ON c.id = p.categoria_id
                LEFT JOIN orden_items oi ON oi.producto_id = p.id
                LEFT JOIN ordenes o ON o.id = oi.orden_id AND o.estado = 'pagada'
                    AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                WHERE p.tenant_id = @tenantId AND p.se_vende = true
                GROUP BY c.nombre, p.costo_promedio
                ORDER BY TotalVentas DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<CostoCategoriaRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            var agrupados = resultados
                .GroupBy(r => r.CategoriaNombre)
                .Select(g => new CostoCategoriaRow(
                    g.Key,
                    (long)g.Sum(r => r.Productos),
                    g.Sum(r => r.TotalVendidos),
                    g.Sum(r => r.TotalVentas),
                    g.Sum(r => r.CostoTotal)
                ))
                .OrderByDescending(r => r.TotalVentas)
                .ToList();

            if (formato == "pdf")
                return await PdfCostoPorCategoria(agrupados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = agrupados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfCostoPorCategoria(List<CostoCategoriaRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var sumVentas = data.Sum(r => r.TotalVentas);
        var sumCosto = data.Sum(r => r.CostoTotal);
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Food Cost por Categoría");
        pdf.Empresa(empresa)
           .Reporte("Food Cost por Categoría")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.CategoriaNombre ?? "N/A",
            r.Productos.ToString("N0"),
            r.TotalVendidos.ToString("N0"),
            r.TotalVentas.ToString("N2"),
            r.CostoTotal.ToString("N2"),
            (r.TotalVentas > 0 ? ((r.CostoTotal / r.TotalVentas) * 100).ToString("N1") + "%" : "N/A")
        });

        pdf.Tabla(
            headers: ["Categoría", "Productos", "Vendidos", "Ventas", "Costo Total", "Food Cost %"],
            rows: rows,
            totalRow: ["Total", "", "", sumVentas.ToString("N2"), sumCosto.ToString("N2"), sumVentas > 0 ? ((sumCosto / sumVentas) * 100).ToString("N1") + "%" : "N/A"]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"costo-por-categoria-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record CostoCategoriaRow(string? CategoriaNombre, long Productos, long TotalVendidos, decimal TotalVentas, decimal CostoTotal);
