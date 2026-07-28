using AuthManager.AspNetCore.Storage;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class SelfContainedPasswordHasherTests
{
    private sealed class DummyUser { }

    [Fact]
    public void HashPassword_then_VerifyHashedPassword_succeeds_for_the_correct_password()
    {
        var hasher = new SelfContainedPasswordHasher<DummyUser>(iterations: 100_000);
        var hash = hasher.HashPassword(new DummyUser(), "correct horse battery staple");

        var result = hasher.VerifyHashedPassword(new DummyUser(), hash, "correct horse battery staple");

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_fails_for_the_wrong_password()
    {
        var hasher = new SelfContainedPasswordHasher<DummyUser>(iterations: 100_000);
        var hash = hasher.HashPassword(new DummyUser(), "correct horse battery staple");

        var result = hasher.VerifyHashedPassword(new DummyUser(), hash, "wrong password");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void Two_hashes_of_the_same_password_are_different_because_the_salt_is_random()
    {
        var hasher = new SelfContainedPasswordHasher<DummyUser>(iterations: 100_000);

        var hash1 = hasher.HashPassword(new DummyUser(), "same-password");
        var hash2 = hasher.HashPassword(new DummyUser(), "same-password");

        Assert.NotEqual(hash1, hash2);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(new DummyUser(), hash1, "same-password"));
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(new DummyUser(), hash2, "same-password"));
    }

    [Fact]
    public void VerifyHashedPassword_flags_rehash_when_the_configured_iteration_count_has_increased()
    {
        var oldHasher = new SelfContainedPasswordHasher<DummyUser>(iterations: 100_000);
        var hash = oldHasher.HashPassword(new DummyUser(), "correct horse battery staple");

        var newHasher = new SelfContainedPasswordHasher<DummyUser>(iterations: 200_000);
        var result = newHasher.VerifyHashedPassword(new DummyUser(), hash, "correct horse battery staple");

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64!!")]
    [InlineData("dGhpcyBpcyB0b28gc2hvcnQ=")] // valid base64, wrong length/marker
    public void VerifyHashedPassword_fails_gracefully_for_malformed_hashes(string malformed)
    {
        var hasher = new SelfContainedPasswordHasher<DummyUser>(iterations: 100_000);

        var result = hasher.VerifyHashedPassword(new DummyUser(), malformed, "anything");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void Constructor_rejects_iteration_counts_below_the_minimum_floor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelfContainedPasswordHasher<DummyUser>(iterations: 1_000));
    }

    [Fact]
    public void Default_iteration_count_matches_ASP_NET_Identitys_own_current_default_strength()
    {
        var hasher = new SelfContainedPasswordHasher<DummyUser>();
        var hash = hasher.HashPassword(new DummyUser(), "password");

        // Decode and check the embedded iteration count directly rather than relying on timing.
        var bytes = Convert.FromBase64String(hash);
        var iterations = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(1, 4));

        Assert.Equal(600_000, iterations);
    }
}
