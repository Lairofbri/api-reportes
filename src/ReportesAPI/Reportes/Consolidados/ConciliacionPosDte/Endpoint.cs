using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Consolidados;

public static class ConciliacionPosDteEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/consolidados/conciliacion-pos-dte", async (
            PosDbContext posDb,
            DteDbContext dteDb,
            DateTime? desde,
            DateTime? hasta) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sqlPos = """
                SELECT o.id AS orden_id,
                       o.numero_orden,
                       o.total,
                       o.creado_en,
                       o.sucursal_id
                FROM ordenes o
                WHERE o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                ORDER BY o.creado_en
                """;

            var ordenesPagadas = await posDb.Database.SqlQueryRaw<OrdenParaConciliar>(
                sqlPos,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            var sqlDte = """
                SELECT d.id AS dte_id,
                       d.codigo_generacion,
                       d.tipo_dte,
                       d.fecha_emision,
                       d.total,
                       d.orden_id
                FROM dtes d
                WHERE d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                  AND d.orden_id IS NOT NULL
                """;

            var dtesEmitidos = await dteDb.Database.SqlQueryRaw<DteParaConciliar>(
                sqlDte,
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            var ordenesConDte = dtesEmitidos.Select(d => d.OrdenId).ToHashSet();
            var ordenesSinDte = ordenesPagadas
                .Where(o => !ordenesConDte.Contains(o.OrdenId))
                .ToList();

            return Results.Ok(new
            {
                periodo = new { desde = @de, hasta = @ha },
                resumen = new
                {
                    total_ordenes_pagadas = ordenesPagadas.Count,
                    total_dtes_emitidos = dtesEmitidos.Count,
                    ordenes_sin_dte = ordenesSinDte.Count,
                    porcentaje_conciliado = ordenesPagadas.Count > 0
                        ? Math.Round((double)(ordenesPagadas.Count - ordenesSinDte.Count) / ordenesPagadas.Count * 100, 2)
                        : 0
                },
                ordenes_sin_dte = ordenesSinDte.Select(o => new
                {
                    o.OrdenId, o.NumeroOrden, o.Total, o.CreadoEn, o.SucursalId
                })
            });
        });
    }
}

public record OrdenParaConciliar(Guid OrdenId, long NumeroOrden, decimal Total, DateTime CreadoEn, Guid? SucursalId);
public record DteParaConciliar(Guid DteId, string CodigoGeneracion, string TipoDte, DateTime FechaEmision, decimal Total, Guid? OrdenId);

