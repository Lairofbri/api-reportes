using Microsoft.EntityFrameworkCore;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Datos;
using ReportesAPI.Reportes.Pos.Ventas;
using ReportesAPI.Reportes.Pos.Productos;
using ReportesAPI.Reportes.Pos.Ordenes;
using ReportesAPI.Reportes.Pos.Propinas;
using ReportesAPI.Reportes.Pos.Caja;
using ReportesAPI.Reportes.Pos.Cocina;
using ReportesAPI.Reportes.Pos.Sucursales;
using ReportesAPI.Reportes.Pos.Staff;
using ReportesAPI.Reportes.Dte.Emisiones;
using ReportesAPI.Reportes.Dte.Establecimientos;
using ReportesAPI.Reportes.Dte.Anulaciones;
using ReportesAPI.Reportes.Dte.Contingencia;
using ReportesAPI.Reportes.Dte.Auditoria;
using ReportesAPI.Reportes.Consolidados;

var builder = WebApplication.CreateBuilder(args);

var connPos = builder.Configuration.GetConnectionString("PosDb")!;
var connDte = builder.Configuration.GetConnectionString("DteDb")!;

builder.Services.AddDbContext<PosDbContext>(o =>
    o.UseNpgsql(connPos).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

builder.Services.AddDbContext<DteDbContext>(o =>
    o.UseNpgsql(connDte).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p
        .WithOrigins(
            "http://localhost:5173",
            "http://localhost:5174",
            "http://localhost:3000",
            "http://localhost:4000",
            "http://localhost:5141",
            "http://localhost:3001")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddScoped<TenantContext>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseCors();
app.UseMiddleware<ReportesAPI.Compartido.Auth.DualAuthMiddleware>();

app.MapGet("/health", () => Results.Ok(new { estado = "ok", timestamp = DateTime.UtcNow }));

// Auth endpoints
ReportesAPI.Features.Auth.LoginEndpoint.Map(app, connPos);
ReportesAPI.Features.Auth.RefreshEndpoint.Map(app, connPos);
ReportesAPI.Features.Auth.LogoutEndpoint.Map(app, connPos);

// POS
VentasPorPeriodoEndpoint.Map(app);
VentasPorMetodoPagoEndpoint.Map(app);
VentasPorSucursalEndpoint.Map(app);
VentasPorTipoOrdenEndpoint.Map(app);
VentasPorOrigenEndpoint.Map(app);
TopProductosVendidosEndpoint.Map(app);
IngresosPorCategoriaEndpoint.Map(app);
StockBajoEndpoint.Map(app);
TicketPromedioEndpoint.Map(app);
HorasPicoEndpoint.Map(app);
OrdenesCanceladasEndpoint.Map(app);
PropinasPorMeseroEndpoint.Map(app);
ResumenDiarioEndpoint.Map(app);
CuadreCajaEndpoint.Map(app);
TiempoPreparacionEndpoint.Map(app);
ComparativaSucursalesEndpoint.Map(app);
DesempenoMeseroEndpoint.Map(app);

// DTE
DtePorPeriodoEndpoint.Map(app);
DtePorTipoEndpoint.Map(app);
DtePorEstadoEndpoint.Map(app);
DteRechazadosEndpoint.Map(app);
MontosFacturadosEndpoint.Map(app);
TasaAceptacionEndpoint.Map(app);
DtePorEstablecimientoEndpoint.Map(app);
DteAnuladosEndpoint.Map(app);
EventosContingenciaEndpoint.Map(app);
PendientesResolucionEndpoint.Map(app);
TasaResolucionEndpoint.Map(app);
LogOperacionesEndpoint.Map(app);

// Consolidados
ConciliacionPosDteEndpoint.Map(app);
IngresosVsDteEndpoint.Map(app);

app.Run();
