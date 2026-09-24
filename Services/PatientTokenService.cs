using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class PatientTokenService(AppDbContext db) : IPatientTokenService
{
    public async Task<string> IssueAsync(int patientAccountId, PatientAuthTokenType type, TimeSpan ttl)
    {
        var rawToken = GenerateToken();
        var now = DateTime.UtcNow;
        if (type == PatientAuthTokenType.Activation)
        {
            var existingTokens = await db.PatientAuthTokens
                .Where(token => token.PatientAccountId == patientAccountId
                    && token.Type == PatientAuthTokenType.Activation
                    && token.ConsumedAt == null)
                .ToListAsync();
            foreach (var token in existingTokens)
                token.ConsumedAt = now;
        }

        db.PatientAuthTokens.Add(new PatientAuthToken
        {
            PatientAccountId = patientAccountId,
            Type             = type,
            TokenHash        = Hash(rawToken),
            ExpiresAt        = now.Add(ttl),
        });
        await db.SaveChangesAsync();

        return rawToken;
    }

    public async Task<PatientAuthToken?> ValidateAsync(string rawToken, PatientAuthTokenType type)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var hash = Hash(rawToken);
        var token = await db.PatientAuthTokens
            .Include(t => t.PatientAccount)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Type == type);

        return token is not null && token.IsUsable(DateTime.UtcNow) ? token : null;
    }

    public async Task ConsumeAsync(PatientAuthToken token)
    {
        token.ConsumedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('='); // url-safe

    private static string Hash(string rawToken)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
}
