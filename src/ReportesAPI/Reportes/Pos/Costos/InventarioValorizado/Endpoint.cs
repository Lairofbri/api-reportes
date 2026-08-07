using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Costos;

public static class InventarioValorizadoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/costos/inventario-valorizado", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? fecha,
            string? formato) =>
        {
            var fechaCorte = fecha ?? DateTime.Today;

            var sql = """
                SELECT
                    p.id AS ProductoId,
                    p.nombre AS ProductoNombre,
                    p.stock_actual AS StockActual,
                    p.costo_promedio AS CostoPromedio,
                    ROUND(CAST(p.stock_actual * p.costo_promedio AS NUMERIC), 2) AS ValorTotal,
                    um.nombre AS Unidad
                FROM productos p
                LEFT JOIN unidades_medida um ON um.id = p.unidad_medida_id
                WHERE p.tenant_id = @tenantId AND p.tiene_stock = true AND p.stock_actual > 0
                ORDER BY ValorTotal DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<InventarioValorizadoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfInventarioValorizado(resultados, fechaCorte, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, fecha = fechaCorte });
        });
    }

    private static async Task<IResult> PdfInventarioValorizado(List<InventarioValorizadoRow> data, DateTime fecha, PosDbContext db, Guid? tenantId)
    {
        var sumValor = data.Sum(r => r.ValorTotal);
        var sumItems = data.Count;
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Inventario Valorizado");
        pdf.Empresa(empresa)
           .Reporte("Inventario Valorizado")
           .Periodo($"Fecha de corte: {fecha:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.ProductoNombre ?? "N/A",
            r.Unidad ?? "",
            r.StockActual.ToString("N2"),
            r.CostoPromedio.ToString("N2"),
            r.ValorTotal.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Producto", "Unidad", "Stock", "Costo Unit.", "Valor Total"],
            rows: rows,
            totalRow: [$"{sumItems} productos", "", "", "", sumValor.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"inventario-valorizado-{fecha:yyyyMMdd}.pdf");
    }
}

public record InventarioValorizadoRow(Guid ProductoId, string ProductoNombre, decimal StockActual, decimal CostoPromedio, decimal ValorTotal, string? Unidad);
