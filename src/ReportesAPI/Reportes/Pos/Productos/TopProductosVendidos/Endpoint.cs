using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Productos;

public static class TopProductosVendidosEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/productos/top", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            int? limite,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;
            var top = limite ?? 20;

            var sql = """
                SELECT oi.producto_id AS ProductoId,
                       P.nombre AS ProductoNombre,
                       Cat.nombre AS CategoriaNombre,
                       SUM(oi.cantidad) AS TotalVendidos,
                       SUM(oi.cantidad * oi.precio_unitario) AS TotalIngresos
                FROM orden_items oi
                JOIN ordenes o ON o.id = oi.orden_id
                LEFT JOIN productos p ON p.id = oi.producto_id
                LEFT JOIN categorias cat ON cat.id = p.categoria_id
                WHERE oi.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                  AND oi.producto_id IS NOT NULL
                GROUP BY oi.producto_id, P.nombre, Cat.nombre
                ORDER BY TotalVendidos DESC
                LIMIT @top
                """;

            var resultados = await db.Database.SqlQueryRaw<TopProductoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha),
                new NpgsqlParameter("@top", top)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfTopProductos(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfTopProductos(List<TopProductoRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var sumVendidos = data.Sum(r => r.TotalVendidos);
        var sumIngresos = data.Sum(r => r.TotalIngresos);
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Top Productos Vendidos");
        pdf.Empresa(empresa)
           .Reporte("Top Productos Vendidos")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.ProductoNombre ?? "N/A",
            r.CategoriaNombre ?? "N/A",
            r.TotalVendidos.ToString("N0"),
            r.TotalIngresos.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Producto", "Categoria", "Vendidos", "Ingresos"],
            rows: rows,
            totalRow: ["Total", "", sumVendidos.ToString("N0"), sumIngresos.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"top-productos-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record TopProductoRow(Guid? ProductoId, string? ProductoNombre, string? CategoriaNombre, long TotalVendidos, decimal TotalIngresos);

