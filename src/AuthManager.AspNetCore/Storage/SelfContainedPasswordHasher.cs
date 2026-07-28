using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace AuthManager.AspNetCore.Storage;

/// <summary>
/// Password hasher used by AuthManager's self-contained storage provider
/// (<see cref="Core.Options.AuthManagerStorageProvider.SelfContained"/>).
///
/// Algorithm: PBKDF2-HMACSHA256, 128-bit random salt, 256-bit derived subkey, configurable
/// iteration count (600,000 by default — matching ASP.NET Identity's own current default
/// strength, itself in line with OWASP's 2023 password-storage guidance). Verification uses a
/// constant-time comparison to avoid leaking timing information about a partial match.
///
/// Encoded format (self-versioned, independent of ASP.NET Identity's own hash format so it
/// can evolve on its own):
///   byte[0]      = format marker (0x01)
///   bytes[1..5)  = iteration count, big-endian Int32
///   bytes[5..21) = 16-byte salt
///   bytes[21..53) = 32-byte PBKDF2 subkey
/// The whole thing is base64-encoded before being stored in the user's PasswordHash column.
/// </summary>
public sealed class SelfContainedPasswordHasher<TUser> : IPasswordHasher<TUser> where TUser : class
{
    private const byte FormatMarker = 0x01;
    private const int SaltSize = 16;
    private const int SubkeySize = 32;
    private const int EncodedSize = 1 + 4 + SaltSize + SubkeySize;

    private readonly int _iterations;

    public SelfContainedPasswordHasher(int iterations = 600_000)
    {
        if (iterations < 100_000)
            throw new ArgumentOutOfRangeException(nameof(iterations),
                "PBKDF2 iteration count must be at least 100,000 — this hasher refuses weaker configurations.");

        _iterations = iterations;
    }

    public string HashPassword(TUser user, string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, _iterations, HashAlgorithmName.SHA256, SubkeySize);

        var output = new byte[EncodedSize];
        output[0] = FormatMarker;
        BinaryPrimitives.WriteInt32BigEndian(output.AsSpan(1, 4), _iterations);
        salt.CopyTo(output.AsSpan(5, SaltSize));
        subkey.CopyTo(output.AsSpan(5 + SaltSize, SubkeySize));

        return Convert.ToBase64String(output);
    }

    public PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(hashedPassword);
        ArgumentNullException.ThrowIfNull(providedPassword);

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(hashedPassword);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }

        if (decoded.Length != EncodedSize || decoded[0] != FormatMarker)
            return PasswordVerificationResult.Failed;

        var storedIterations = BinaryPrimitives.ReadInt32BigEndian(decoded.AsSpan(1, 4));
        var salt = decoded.AsSpan(5, SaltSize).ToArray();
        var expectedSubkey = decoded.AsSpan(5 + SaltSize, SubkeySize).ToArray();

        var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(providedPassword), salt, storedIterations, HashAlgorithmName.SHA256, SubkeySize);

        if (!CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey))
            return PasswordVerificationResult.Failed;

        return storedIterations < _iterations
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }
}
