using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Staff;

public static class DesempenoMeseroEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/staff/desempeno-mesero", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT o.usuario_id AS UsuarioId,
                       U.nombre AS MeseroNombre,
                       COUNT(DISTINCT o.id) AS TotalOrdenes,
                       ROUND(SUM(o.total)::numeric, 2) AS TotalVentas,
                       ROUND(AVG(o.total)::numeric, 2) AS TicketPromedio,
                       ROUND(SUM(o.propina)::numeric, 2) AS TotalPropinas
                FROM ordenes o
                LEFT JOIN usuarios u ON u.id = o.usuario_id
                WHERE o.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY o.usuario_id, U.nombre
                ORDER BY TotalVentas DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DesempenoMeseroRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfDesempenoMesero(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfDesempenoMesero(List<DesempenoMeseroRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var totalOrdenes = data.Sum(r => r.TotalOrdenes);
        var totalVentas = data.Sum(r => r.TotalVentas);
        var ticketPromedio = totalOrdenes > 0 ? totalVentas / totalOrdenes : 0;
        var totalPropinas = data.Sum(r => r.TotalPropinas);
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Desempeno de Meseros");
        pdf.Empresa(empresa)
           .Reporte("Desempeno de Meseros")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.MeseroNombre ?? "Sin nombre",
            r.TotalOrdenes.ToString("N0"),
            r.TotalVentas.ToString("N2"),
            r.TicketPromedio.ToString("N2"),
            r.TotalPropinas.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Mesero", "Ordenes", "Ventas", "Ticket Promedio", "Propinas"],
            rows: rows,
            totalRow: ["Total", totalOrdenes.ToString("N0"), totalVentas.ToString("N2"), ticketPromedio.ToString("N2"), totalPropinas.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"desempeno-mesero-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record DesempenoMeseroRow(Guid UsuarioId, string? MeseroNombre, long TotalOrdenes, decimal TotalVentas, decimal TicketPromedio, decimal TotalPropinas);

