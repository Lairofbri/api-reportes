using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Propinas;

public static class PropinasPorMeseroEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/propinas/por-mesero", async (
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
                       ROUND(SUM(o.propina)::numeric, 2) AS TotalPropinas,
                       ROUND(AVG(o.propina)::numeric, 2) AS PropinaPromedio
                FROM ordenes o
                LEFT JOIN usuarios u ON u.id = o.usuario_id
                WHERE o.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                  AND o.propina > 0
                GROUP BY o.usuario_id, U.nombre
                ORDER BY TotalPropinas DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<PropinaMeseroRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfPropinasPorMesero(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfPropinasPorMesero(List<PropinaMeseroRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var totalOrdenes = data.Sum(r => r.TotalOrdenes);
        var totalPropinas = data.Sum(r => r.TotalPropinas);
        var promedioGeneral = totalOrdenes > 0 ? totalPropinas / totalOrdenes : 0;
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Propinas por Mesero");
        pdf.Empresa(empresa)
           .Reporte("Propinas por Mesero")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.MeseroNombre ?? "Sin nombre",
            r.TotalOrdenes.ToString("N0"),
            r.TotalPropinas.ToString("N2"),
            r.PropinaPromedio.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Mesero", "Ordenes", "Total Propinas", "Promedio"],
            rows: rows,
            totalRow: ["Total", totalOrdenes.ToString("N0"), totalPropinas.ToString("N2"), promedioGeneral.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"propinas-por-mesero-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record PropinaMeseroRow(Guid UsuarioId, string? MeseroNombre, long TotalOrdenes, decimal TotalPropinas, decimal PropinaPromedio);

