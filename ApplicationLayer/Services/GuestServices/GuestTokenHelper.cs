using System.Security.Cryptography;
using System.Text;

namespace Application.Services.GuestServices;

public static class GuestTokenHelper
{
    public static string GenerateGuestToken()
    {
        var tokenBytes = new byte[32]; 
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);
        return Convert.ToBase64String(tokenBytes);
    }

    public static string HashGuestToken(string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(tokenBytes);
        return Convert.ToBase64String(hashBytes);
    }
}
