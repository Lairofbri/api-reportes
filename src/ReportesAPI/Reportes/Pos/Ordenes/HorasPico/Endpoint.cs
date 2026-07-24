using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ordenes;

public static class HorasPicoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ordenes/horas-pico", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT EXTRACT(HOUR FROM o.creado_en)::int AS Hora,
                       COUNT(DISTINCT o.id) AS TotalOrdenes,
                       ROUND(SUM(o.total)::numeric, 2) AS TotalIngresos
                FROM ordenes o
                WHERE o.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY EXTRACT(HOUR FROM o.creado_en)
                ORDER BY Hora
                """;

            var resultados = await db.Database.SqlQueryRaw<HorasPicoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfHorasPico(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfHorasPico(List<HorasPicoRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var totalOrdenes = data.Sum(r => r.TotalOrdenes);
        var totalIngresos = data.Sum(r => r.TotalIngresos);
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Horas Pico");
        pdf.Empresa(empresa)
           .Reporte("Horas Pico")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            $"{r.Hora:D2}:00",
            r.TotalOrdenes.ToString("N0"),
            r.TotalIngresos.ToString("N2"),
            totalOrdenes > 0 ? ((double)r.TotalOrdenes / totalOrdenes * 100).ToString("F1") + "%" : "0%"
        });

        pdf.Tabla(
            headers: ["Hora", "Ordenes", "Ingresos", "% del Dia"],
            rows: rows,
            totalRow: ["Total", totalOrdenes.ToString("N0"), totalIngresos.ToString("N2"), "100%"]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"horas-pico-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record HorasPicoRow(int Hora, long TotalOrdenes, decimal TotalIngresos);

