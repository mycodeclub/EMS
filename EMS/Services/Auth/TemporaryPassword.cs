using System.Security.Cryptography;

namespace EMS.Services.Auth;

/// <summary>Passwords for logins created on someone's behalf (trial owners, employees). They are told to change it.</summary>
public static class TemporaryPassword
{
    /// <summary>12 characters with upper, lower, digit and symbol, e.g. "Kmt@4821Pqw7"; avoids look-alike characters.</summary>
    public static string Create()
    {
        static string Pick(string chars, int count) =>
            new(Enumerable.Range(0, count).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnpqrstuvwxyz", digits = "23456789";
        return Pick(upper, 1) + Pick(lower, 2) + "@" + Pick(digits, 4) + Pick(upper, 1) + Pick(lower, 2) + Pick(digits, 1);
    }
}
