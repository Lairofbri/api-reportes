using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Productos;

public static class IngresosPorCategoriaEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/productos/ingresos-por-categoria", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT cat.id AS CategoriaId,
                       Cat.nombre AS CategoriaNombre,
                       COUNT(DISTINCT oi.id) AS TotalItems,
                       SUM(oi.cantidad * oi.precio_unitario) AS TotalIngresos
                FROM orden_items oi
                JOIN ordenes o ON o.id = oi.orden_id
                LEFT JOIN productos p ON p.id = oi.producto_id
                LEFT JOIN categorias cat ON cat.id = p.categoria_id
                WHERE oi.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                  AND oi.producto_id IS NOT NULL
                GROUP BY cat.id, Cat.nombre
                ORDER BY TotalIngresos DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<IngresosCategoriaRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfIngresosPorCategoria(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfIngresosPorCategoria(List<IngresosCategoriaRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var sumProductos = data.Sum(r => r.TotalItems);
        var sumIngresos = data.Sum(r => r.TotalIngresos);
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Ingresos por Categoria");
        pdf.Empresa(empresa)
           .Reporte("Ingresos por Categoria")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.CategoriaNombre ?? "N/A",
            r.TotalItems.ToString("N0"),
            r.TotalIngresos.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Categoria", "Productos", "Ingresos"],
            rows: rows,
            totalRow: ["Total", sumProductos.ToString("N0"), sumIngresos.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"ingresos-por-categoria-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record IngresosCategoriaRow(Guid? CategoriaId, string? CategoriaNombre, long TotalItems, decimal TotalIngresos);

