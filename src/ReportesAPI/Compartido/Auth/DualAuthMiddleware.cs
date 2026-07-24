using System.Security.Claims;
using ReportesAPI.Compartido.MultiTenant;

namespace ReportesAPI.Compartido.Auth;

public class DualAuthMiddleware
{
    private readonly RequestDelegate _next;

    public DualAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, IConfiguration config)
    {
        var path = context.Request.Path.Value ?? "";

        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (!string.IsNullOrEmpty(apiKey))
        {
            if (!await TryAuthenticateApiKey(context, apiKey, config, tenantContext))
                return;
        }
        else if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader["Bearer ".Length..].Trim();

            if (!TryAuthenticateJwt(token, config, tenantContext))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Token inválido o expirado." });
                return;
            }
        }
        else
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Autenticación requerida. Usa X-API-Key o Authorization: Bearer <token>" });
            return;
        }

        await _next(context);
    }

    private static bool IsPublicPath(string path)
    {
        return path.StartsWith("/health") ||
               path.StartsWith("/api/auth/login") ||
               path.StartsWith("/api/auth/refresh") ||
               path.StartsWith("/api/auth/logout");
    }

    private static bool TryAuthenticateJwt(string token, IConfiguration config, TenantContext tenantContext)
    {
        var jwtSecret = config["JWT_SECRET"];

        if (string.IsNullOrEmpty(jwtSecret))
        {
            return false;
        }

        var principal = JwtHelper.ValidateToken(token, jwtSecret);

        if (principal is null)
            return false;

        tenantContext.AuthMode = "jwt";
        tenantContext.UsuarioId = JwtHelper.GetClaim(principal, "sub");
        tenantContext.Rol = JwtHelper.GetClaim(principal, "rol");
        tenantContext.Email = JwtHelper.GetClaim(principal, "email");

        var tenantId = JwtHelper.GetClaim(principal, "tenant_id");
        if (Guid.TryParse(tenantId, out var tid))
            tenantContext.TenantId = tid;

        var sucursalId = JwtHelper.GetClaim(principal, "sucursal_id");
        if (Guid.TryParse(sucursalId, out var sid))
            tenantContext.SucursalId = sid;

        var establecimientoId = JwtHelper.GetClaim(principal, "establecimiento_id");
        if (Guid.TryParse(establecimientoId, out var eid))
            tenantContext.EstablecimientoId = eid;

        return true;
    }

    private static async Task<bool> TryAuthenticateApiKey(HttpContext context, string apiKey, IConfiguration config, TenantContext tenantContext)
    {
        var expectedHash = config["API_KEY_HASH"];

        if (string.IsNullOrEmpty(expectedHash))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API Key no configurada en el servidor." });
            return false;
        }

        if (!BCrypt.Net.BCrypt.Verify(apiKey, expectedHash))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API Key inválida." });
            return false;
        }

        tenantContext.AuthMode = "apikey";
        tenantContext.Rol = "admin";

        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (Guid.TryParse(tenantId, out var tid))
            tenantContext.TenantId = tid;

        return true;
    }
}