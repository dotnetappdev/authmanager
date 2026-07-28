using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManagerSample.WebApi.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthManagerSample.WebApi.Data;

/// <summary>
/// Populates the sample with demo data on startup: a handful of ASP.NET Identity users
/// spread across roles (Admin, Customer, Reader, Viewer — SuperAdmin is seeded separately
/// by AuthManager itself), plus demo rows in the licensing entities (Customers, License
/// Keys, Subscription Plans/Subscriptions, Customer API Keys, OAuth Clients) so the admin
/// UI has something to show beyond an empty database. Idempotent — skips whatever already
/// exists, so it's safe to run on every startup.
/// </summary>
public static class DemoSeeder
{
    private static readonly (string Role, string Email, string Password)[] DemoUsers =
    [
        ("Admin",    "admin@example.com",    "Admin@123456!"),
        ("Customer", "customer@example.com", "Customer@123456!"),
        ("Reader",   "reader@example.com",   "Reader@123456!"),
        ("Viewer",   "viewer@example.com",   "Viewer@123456!"),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        await SeedIdentityAsync(sp);
        await SeedLicensingAsync(sp);
    }

    /// <summary>
    /// Deletes every demo licensing row (customers, license keys, subscription plans and
    /// subscriptions, customer API keys, OAuth clients) so the sample starts clean again.
    /// Does <b>not</b> touch ASP.NET Identity — the SuperAdmin and Admin/Customer/Reader/Viewer
    /// demo accounts, their roles, and their passwords are left exactly as they are. Call
    /// <see cref="SeedAsync"/> afterwards to repopulate the licensing demo data if you want it
    /// back (the Identity accounts won't be recreated — they're skipped because they already exist).
    /// </summary>
    public static async Task PurgeLicensingDataAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        // CustomerSubscriptions have no dedicated delete endpoint — cancelling is a soft-delete
        // by design, and a cancelled subscription still blocks DeletePlanAsync below (it checks
        // for any row referencing the plan, not just active ones). Go straight to the DbContext
        // for this one row type since a full purge is a maintenance operation, not something a
        // normal admin action needs to do.
        var dbFactory = sp.GetRequiredService<IDbContextFactory<AuthManagerDbContext>>();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.CustomerSubscriptions.RemoveRange(db.CustomerSubscriptions);
            await db.SaveChangesAsync();
        }

        var subscriptions = sp.GetRequiredService<ISubscriptionService>();
        foreach (var plan in await subscriptions.GetPlansAsync())
            await subscriptions.DeletePlanAsync(plan.Id);

        var licenses = sp.GetRequiredService<ILicenseService>();
        foreach (var license in await licenses.GetLicensesAsync())
            await licenses.DeleteLicenseAsync(license.Id);

        var apiKeys = sp.GetRequiredService<ICustomerApiKeyService>();
        foreach (var key in await apiKeys.GetKeysAsync())
            await apiKeys.DeleteKeyAsync(key.Id);

        var oauthClients = sp.GetRequiredService<IOAuthClientService>();
        foreach (var client in await oauthClients.GetClientsAsync())
            await oauthClients.DeleteClientAsync(client.Id);

        var customers = sp.GetRequiredService<ICustomerService>();
        foreach (var customer in await customers.GetCustomersAsync())
            await customers.DeleteCustomerAsync(customer.Id);
    }

    // ── ASP.NET Identity: roles + one demo user per role ─────────────────────
    private static async Task SeedIdentityAsync(IServiceProvider sp)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var (role, email, password) in DemoUsers)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }

            if (await userManager.FindByEmailAsync(email) is not null) continue;

            var user = new ApplicationUser
            {
                UserName       = email.Split('@')[0],
                Email          = email,
                EmailConfirmed = true,
                FirstName      = role,
                LastName       = "Demo",
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    // ── Licensing: customers, license keys, subscriptions, API keys, OAuth clients ──
    private static async Task SeedLicensingAsync(IServiceProvider sp)
    {
        // Called before app.Run(), so AuthManagerDbInitialiser (an IHostedService) hasn't
        // created the schema yet — do it here so the licensing services below have tables
        // to write to.
        var factory = sp.GetRequiredService<IDbContextFactory<AuthManagerDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        var customers     = sp.GetRequiredService<ICustomerService>();
        var licenses      = sp.GetRequiredService<ILicenseService>();
        var subscriptions = sp.GetRequiredService<ISubscriptionService>();
        var apiKeys       = sp.GetRequiredService<ICustomerApiKeyService>();
        var oauthClients  = sp.GetRequiredService<IOAuthClientService>();

        if ((await customers.GetCustomersAsync()).Count > 0) return;

        var demoCustomers = new (string Name, string Email, string Company)[]
        {
            ("Wile E. Coyote", "wile.coyote@acme-corp.example",  "Acme Corp"),
            ("Hank Scorpio",   "hank@globex.example",            "Globex Inc"),
            ("Bill Lumbergh",  "bill.lumbergh@initech.example",  "Initech"),
        };

        var createdCustomers = new List<CustomerDto>();
        foreach (var (name, email, company) in demoCustomers)
        {
            var (success, _, customer) = await customers.CreateCustomerAsync(new CreateCustomerDto
            {
                Name        = name,
                Email       = email,
                CompanyName = company,
            });
            if (success && customer is not null) createdCustomers.Add(customer);
        }

        if (createdCustomers.Count == 0) return;

        // ── Subscription plans + subscribe the first two customers ──
        var (_, _, starterPlan) = await subscriptions.CreatePlanAsync(new CreateSubscriptionPlanDto
        {
            Name        = "Starter",
            Description = "Single-seat plan for small teams",
            PriceCents  = 1900,
            Currency    = "USD",
            Interval    = BillingInterval.Monthly,
            TrialDays   = 14,
            MaxApiKeys  = 2,
            Features    = ["1 project", "Email support"],
        });

        var (_, _, proPlan) = await subscriptions.CreatePlanAsync(new CreateSubscriptionPlanDto
        {
            Name        = "Pro",
            Description = "For growing teams that need more headroom",
            PriceCents  = 4900,
            Currency    = "USD",
            Interval    = BillingInterval.Monthly,
            TrialDays   = 14,
            MaxApiKeys  = 10,
            Features    = ["Unlimited projects", "Priority support", "SSO"],
        });

        if (starterPlan is not null && createdCustomers.Count > 0)
        {
            await subscriptions.SubscribeAsync(new CreateCustomerSubscriptionDto
            {
                CustomerId = createdCustomers[0].Id,
                PlanId     = starterPlan.Id,
            });
        }

        if (proPlan is not null && createdCustomers.Count > 1)
        {
            await subscriptions.SubscribeAsync(new CreateCustomerSubscriptionDto
            {
                CustomerId = createdCustomers[1].Id,
                PlanId     = proPlan.Id,
            });
        }

        // ── License keys, one per demo customer ──
        foreach (var customer in createdCustomers)
        {
            await licenses.CreateLicenseAsync(new CreateLicenseKeyDto
            {
                ProductName    = "Desktop App",
                CustomerId     = customer.Id,
                MaxActivations = 3,
            });
        }

        // ── A customer-facing API key for the first customer ──
        await apiKeys.CreateKeyAsync(new CreateCustomerApiKeyDto
        {
            CustomerId         = createdCustomers[0].Id,
            Name                = "Production key",
            Scopes              = ["read", "write"],
            RateLimitPerMinute  = 120,
        });

        // ── Third-party service-to-service OAuth clients ──
        await oauthClients.CreateClientAsync(new CreateOAuthClientDto
        {
            ClientId       = "billing-service",
            Name           = "Billing Service",
            Description    = "Internal billing microservice — client-credentials grant",
            AllowedScopes  = ["billing:read", "billing:write"],
        });

        await oauthClients.CreateClientAsync(new CreateOAuthClientDto
        {
            ClientId       = "partner-mobile-app",
            Name           = "Partner Mobile App",
            Description    = "Third-party mobile integration partner",
            AllowedScopes  = ["profile:read"],
        });
    }
}
