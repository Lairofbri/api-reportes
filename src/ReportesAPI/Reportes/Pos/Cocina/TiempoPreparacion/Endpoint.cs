using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Cocina;

public static class TiempoPreparacionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/cocina/tiempo-preparacion", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT oi.producto_id AS ProductoId,
                       P.nombre AS ProductoNombre,
                       Cat.nombre AS CategoriaNombre,
                       COUNT(oi.id) AS TotalItems,
                       ROUND(AVG(EXTRACT(EPOCH FROM (COALESCE(oi.actualizado_en, Oi.creado_en) - oi.creado_en)) / 60)::numeric, 2) AS MinutosPromedio
                FROM orden_items oi
                JOIN ordenes o ON o.id = oi.orden_id
                LEFT JOIN productos p ON p.id = oi.producto_id
                LEFT JOIN categorias cat ON cat.id = p.categoria_id
                WHERE oi.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND oi.estado IN ('listo', 'cancelado')
                  AND oi.producto_id IS NOT NULL
                GROUP BY oi.producto_id, P.nombre, Cat.nombre
                ORDER BY MinutosPromedio DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<TiempoPrepRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfTiempoPreparacion(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfTiempoPreparacion(List<TiempoPrepRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var totalItems = data.Sum(r => r.TotalItems);
        var promedioGeneral = data.Count > 0 ? data.Average(r => r.MinutosPromedio) : 0;
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Tiempo de Preparacion");
        pdf.Empresa(empresa)
           .Reporte("Tiempo de Preparacion")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.ProductoNombre ?? "Sin nombre",
            r.CategoriaNombre ?? "Sin categoria",
            r.TotalItems.ToString("N0"),
            r.MinutosPromedio.ToString("F2")
        });

        pdf.Tabla(
            headers: ["Producto", "Categoria", "Items", "Minutos Promedio"],
            rows: rows,
            totalRow: ["Total", "", totalItems.ToString("N0"), promedioGeneral.ToString("F2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"tiempo-preparacion-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record TiempoPrepRow(Guid? ProductoId, string? ProductoNombre, string? CategoriaNombre, long TotalItems, decimal MinutosPromedio);

