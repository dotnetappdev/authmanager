using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class LicenseServiceTests : ServiceTestBase
{
    [Fact]
    public async Task CreateLicenseAsync_generates_a_formatted_key()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILicenseService>();

        var (ok, errors, license) = await svc.CreateLicenseAsync(new CreateLicenseKeyDto { ProductName = "Acme Pro" });

        Assert.True(ok);
        Assert.Empty(errors);
        Assert.Matches(@"^[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}$", license!.Key);
        Assert.Equal(LicenseStatus.Active, license.Status);
    }

    [Fact]
    public async Task ValidateLicenseAsync_reports_not_found_for_an_unknown_key()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILicenseService>();

        var result = await svc.ValidateLicenseAsync("NOPE-NOPE-NOPE-NOPE");

        Assert.False(result.Valid);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task ActivateLicenseAsync_registers_a_machine_and_is_idempotent_for_the_same_machine()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILicenseService>();
        var (_, _, license) = await svc.CreateLicenseAsync(new CreateLicenseKeyDto { ProductName = "Acme Pro", MaxActivations = 2 });

        var (ok1, _, _) = await svc.ActivateLicenseAsync(license!.Key, "machine-a");
        var (ok2, _, _) = await svc.ActivateLicenseAsync(license.Key, "machine-a"); // same machine again

        Assert.True(ok1);
        Assert.True(ok2);
        var fetched = await svc.GetLicenseAsync(license.Id);
        Assert.Equal(1, fetched!.ActivationCount); // still just one distinct activation
    }

    [Fact]
    public async Task ActivateLicenseAsync_fails_once_MaxActivations_is_reached()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILicenseService>();
        var (_, _, license) = await svc.CreateLicenseAsync(new CreateLicenseKeyDto { ProductName = "Acme Pro", MaxActivations = 1 });
        await svc.ActivateLicenseAsync(license!.Key, "machine-a");

        var (ok, errors, result) = await svc.ActivateLicenseAsync(license.Key, "machine-b");

        Assert.False(ok);
        Assert.NotEmpty(errors);
        Assert.False(result.Valid);
    }

    [Fact]
    public async Task DeactivateLicenseAsync_frees_up_a_slot()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILicenseService>();
        var (_, _, license) = await svc.CreateLicenseAsync(new CreateLicenseKeyDto { ProductName = "Acme Pro", MaxActivations = 1 });
        await svc.ActivateLicenseAsync(license!.Key, "machine-a");

        var (deactivated, _) = await svc.DeactivateLicenseAsync(license.Key, "machine-a");
        var (reactivated, _, _) = await svc.ActivateLicenseAsync(license.Key, "machine-b");

        Assert.True(deactivated);
        Assert.True(reactivated);
    }

    [Fact]
    public async Task RevokeLicenseAsync_makes_validation_fail()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILicenseService>();
        var (_, _, license) = await svc.CreateLicenseAsync(new CreateLicenseKeyDto { ProductName = "Acme Pro" });

        var (ok, _) = await svc.RevokeLicenseAsync(license!.Id);
        var result = await svc.ValidateLicenseAsync(license.Key);

        Assert.True(ok);
        Assert.False(result.Valid);
    }

    [Fact]
    public async Task An_expired_license_fails_validation()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILicenseService>();
        var (_, _, license) = await svc.CreateLicenseAsync(new CreateLicenseKeyDto
        {
            ProductName = "Acme Pro",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        var result = await svc.ValidateLicenseAsync(license!.Key);

        Assert.False(result.Valid);
    }

    [Fact]
    public async Task DeleteLicenseAsync_removes_it_and_its_activations()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILicenseService>();
        var (_, _, license) = await svc.CreateLicenseAsync(new CreateLicenseKeyDto { ProductName = "Acme Pro" });
        await svc.ActivateLicenseAsync(license!.Key, "machine-a");

        var (ok, _) = await svc.DeleteLicenseAsync(license.Id);

        Assert.True(ok);
        Assert.Null(await svc.GetLicenseAsync(license.Id));
    }
}
