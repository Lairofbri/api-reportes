using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Productos;

public static class StockBajoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/productos/stock-bajo", async (
            PosDbContext db,
            TenantContext tenantContext,
            int? umbral,
            string? formato) =>
        {
            var minStock = umbral ?? 10;

            var sql = """
                SELECT p.id AS ProductoId,
                       p.nombre AS ProductoNombre,
                       cat.nombre AS CategoriaNombre,
                       p.stock
                FROM productos p
                LEFT JOIN categorias cat ON cat.id = p.categoria_id
                WHERE p.tenant_id = @tenantId
                  AND p.activo = true AND p.stock <= @minStock
                ORDER BY P.stock ASC
                """;

            var resultados = await db.Database.SqlQueryRaw<StockBajoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@minStock", minStock)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfStockBajo(resultados, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, umbral = minStock });
        });
    }

    private static async Task<IResult> PdfStockBajo(List<StockBajoRow> data, PosDbContext db, Guid? tenantId)
    {
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Productos con Stock Bajo");
        pdf.Empresa(empresa)
           .Reporte("Productos con Stock Bajo")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.ProductoNombre ?? "N/A",
            r.CategoriaNombre ?? "N/A",
            r.Stock.ToString("N0")
        });

        pdf.Tabla(
            headers: ["Producto", "Categoria", "Stock Actual"],
            rows: rows,
            totalRow: ["Total productos: " + data.Count.ToString(), "", ""]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"stock-bajo-{DateTime.Now:yyyyMMdd}.pdf");
    }
}

public record StockBajoRow(Guid ProductoId, string ProductoNombre, string? CategoriaNombre, int Stock);

