using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ReportesAPI.Compartido.Auth;

public static class JwtHelper
{
    public static ClaimsPrincipal? ValidateToken(string token, string secret)
    {
        if (secret.Length < 64)
            throw new InvalidOperationException("JWT_SECRET debe tener al menos 64 caracteres.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var validator = new JwtSecurityTokenHandler();

        try
        {
            return validator.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);
        }
        catch
        {
            return null;
        }
    }

    public static string GetClaim(ClaimsPrincipal principal, string claimType)
    {
        return principal.FindFirst(claimType)?.Value ?? string.Empty;
    }
}