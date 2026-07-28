using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManagerSample.WebApi.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthManagerSample.WebApi.Data;

/// <summary>
/// Populates the sample with demo data on startup at a more realistic volume: the four
/// documented demo accounts (Admin, Customer, Reader, Viewer — SuperAdmin is seeded
/// separately by AuthManager itself) plus a further ~26 generated users spread across
/// roles, some locked out / unconfirmed / 2FA-enabled for variety; and demo rows in the
/// licensing entities (Customers, License Keys, Subscription Plans/Subscriptions, Customer
/// API Keys, OAuth Clients) so the admin UI has enough rows to page, sort, and filter
/// through instead of a handful of empty-feeling records. Idempotent — skips whatever
/// already exists, so it's safe to run on every startup.
/// </summary>
public static class DemoSeeder
{
    // The 4 accounts documented in the README's demo-credentials table — keep these exact
    // emails/passwords/roles, since users copy them straight out of the docs to sign in.
    private static readonly (string Role, string Email, string Password)[] DemoUsers =
    [
        ("Admin",    "admin@example.com",    "Admin@123456!"),
        ("Customer", "customer@example.com", "Customer@123456!"),
        ("Reader",   "reader@example.com",   "Reader@123456!"),
        ("Viewer",   "viewer@example.com",   "Viewer@123456!"),
    ];

    // Additional generated users to bring the Users/Sessions/Sign-in History pages up to a
    // realistic volume. Password is the same for all of them (demo data only) — pattern
    // matches the documented demo accounts' complexity so it satisfies the default policy.
    private const string BulkUserPassword = "Passw0rd!2345";

    private static readonly (string First, string Last, string Role, bool Locked, bool Unconfirmed, bool TwoFactor)[] BulkUsers =
    [
        ("Alice",    "Johnson",   "Customer", false, false, false),
        ("Bob",      "Martinez",  "Customer", false, false, false),
        ("Carol",    "Chen",      "Customer", false, true,  false),
        ("David",    "Kim",       "Customer", false, false, false),
        ("Emma",     "Wilson",    "Customer", true,  false, false),
        ("Frank",    "Rodriguez", "Customer", false, false, false),
        ("Grace",    "Lee",       "Customer", false, false, true),
        ("Henry",    "Patel",     "Customer", false, false, false),
        ("Isabella", "Garcia",    "Customer", false, true,  false),
        ("Jack",     "Thompson",  "Customer", false, false, false),
        ("Karen",    "Nguyen",    "Reader",   false, false, false),
        ("Liam",     "O'Brien",   "Reader",   false, false, false),
        ("Maria",    "Santos",    "Reader",   true,  false, false),
        ("Noah",     "Anderson",  "Reader",   false, false, false),
        ("Olivia",   "Brown",     "Reader",   false, false, false),
        ("Peter",    "Zhang",     "Reader",   false, false, true),
        ("Quinn",    "Sullivan",  "Reader",   false, false, false),
        ("Rachel",   "Davis",     "Reader",   false, true,  false),
        ("Samuel",   "Okafor",    "Viewer",   false, false, false),
        ("Tina",     "Kowalski",  "Viewer",   false, false, false),
        ("Uma",      "Sharma",    "Viewer",   false, false, false),
        ("Victor",   "Petrov",    "Viewer",   false, false, false),
        ("Wendy",    "Clarke",    "Viewer",   false, false, false),
        ("Xavier",   "Silva",     "Viewer",   false, false, false),
        ("Yara",     "Haddad",    "Admin",    false, false, false),
        ("Zoe",      "Fischer",   "Admin",    false, false, false),
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
    /// Does <b>not</b> touch ASP.NET Identity — the SuperAdmin and all demo/bulk accounts,
    /// their roles, and their passwords are left exactly as they are. Call
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

    // ── ASP.NET Identity: roles + the documented demo users + a bulk batch for volume ──
    private static async Task SeedIdentityAsync(IServiceProvider sp)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

        // SuperAdmin itself is created by AuthManager's own SuperAdminSeeder hosted service
        // (options.SeedSuperAdmin = true, below in Program.cs) — that's what actually creates
        // the superadmin@example.com user. The role is ensured here too so it always exists
        // the moment Identity is up, regardless of hosted-service startup ordering.
        foreach (var role in new[] { "SuperAdmin", "Admin", "Customer", "Reader", "Viewer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        foreach (var (role, email, password) in DemoUsers)
        {
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
                await userManager.AddToRoleAsync(user, role);
        }

        foreach (var (first, last, role, locked, unconfirmed, twoFactor) in BulkUsers)
        {
            var email = $"{first.ToLowerInvariant().Replace("'", "")}.{last.ToLowerInvariant().Replace("'", "")}@example.com";
            if (await userManager.FindByEmailAsync(email) is not null) continue;

            var user = new ApplicationUser
            {
                UserName       = $"{first.ToLowerInvariant().Replace("'", "")}.{last.ToLowerInvariant().Replace("'", "")}",
                Email          = email,
                EmailConfirmed = !unconfirmed,
                FirstName      = first,
                LastName       = last,
            };

            var result = await userManager.CreateAsync(user, BulkUserPassword);
            if (!result.Succeeded) continue;

            await userManager.AddToRoleAsync(user, role);

            if (locked)
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddDays(7));

            if (twoFactor)
                await userManager.SetTwoFactorEnabledAsync(user, true);
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

        // A realistic customer roster: the 3 original pop-culture contacts, plus Microsoft's
        // own long-standing set of placeholder company names (Contoso, Fabrikam, Northwind,
        // etc.) — deliberately fictional, trademark-free, and exactly what they're designed for.
        var demoCustomers = new (string Name, string Email, string Company)[]
        {
            ("Wile E. Coyote",  "wile.coyote@acme-corp.example",   "Acme Corp"),
            ("Hank Scorpio",    "hank@globex.example",             "Globex Inc"),
            ("Bill Lumbergh",   "bill.lumbergh@initech.example",   "Initech"),
            ("Nancy Davolio",   "nancy.davolio@contoso.example",   "Contoso Ltd"),
            ("Andrew Fuller",   "andrew.fuller@fabrikam.example",  "Fabrikam Inc"),
            ("Janet Leverling", "janet.leverling@northwind.example", "Northwind Traders"),
            ("Steven Buchanan", "steven.buchanan@adventure-works.example", "AdventureWorks Cycles"),
            ("Laura Callahan",  "laura.callahan@tailspintoys.example", "Tailspin Toys"),
            ("Anne Dodsworth",  "anne.dodsworth@woodgrovebank.example", "Woodgrove Bank"),
            ("Michael Suyama",  "michael.suyama@litware.example",  "Litware Inc"),
            ("Robert King",     "robert.king@wingtiptoys.example", "Wingtip Toys"),
            ("Margaret Peacock", "margaret.peacock@proseware.example", "Proseware Inc"),
            ("Nige White",      "nige.white@treyresearch.example", "Trey Research"),
            ("Paul Nakamura",   "paul.nakamura@fourthcoffee.example", "Fourth Coffee"),
            ("Susan Ito",       "susan.ito@relecloud.example",     "Relecloud"),
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

        // ── Subscription plans, three tiers ──
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

        var (_, _, enterprisePlan) = await subscriptions.CreatePlanAsync(new CreateSubscriptionPlanDto
        {
            Name        = "Enterprise",
            Description = "Custom limits, dedicated support, and an annual contract",
            PriceCents  = 199900,
            Currency    = "USD",
            Interval    = BillingInterval.Yearly,
            TrialDays   = 30,
            MaxApiKeys  = 100,
            Features    = ["Unlimited projects", "Dedicated support", "SSO", "SLA", "Custom contract"],
        });

        // Distribute subscriptions across plans and statuses — most active (some skipping the
        // trial), a couple left mid-trial, and one cancelled — so the Subscriptions page shows
        // a realistic mix instead of every row looking identical.
        var plans = new[] { starterPlan, proPlan, enterprisePlan, starterPlan, proPlan, starterPlan, proPlan, enterprisePlan };
        for (var i = 0; i < createdCustomers.Count && i < plans.Length; i++)
        {
            var plan = plans[i];
            if (plan is null) continue;

            var (ok, _, subscription) = await subscriptions.SubscribeAsync(new CreateCustomerSubscriptionDto
            {
                CustomerId = createdCustomers[i].Id,
                PlanId     = plan.Id,
                SkipTrial  = i % 3 != 1, // every third customer stays in trial; the rest go straight to Active
            });

            // Cancel one subscription outright so Subscriptions shows a Canceled row too.
            if (ok && subscription is not null && i == plans.Length - 1)
                await subscriptions.CancelSubscriptionAsync(subscription.Id);
        }

        // ── License keys — most customers get one, a couple of larger accounts get two ──
        foreach (var (customer, index) in createdCustomers.Select((c, idx) => (c, idx)))
        {
            await licenses.CreateLicenseAsync(new CreateLicenseKeyDto
            {
                ProductName    = "Desktop App",
                CustomerId     = customer.Id,
                MaxActivations = 3,
            });

            if (index is 0 or 3)
            {
                await licenses.CreateLicenseAsync(new CreateLicenseKeyDto
                {
                    ProductName    = "Mobile App",
                    CustomerId     = customer.Id,
                    MaxActivations = 5,
                });
            }
        }

        // ── Customer-facing API keys for the first half of the roster ──
        var apiKeyNames = new[] { "Production key", "Staging key", "CI key", "Analytics key", "Mobile key", "Backup key", "Webhook relay key" };
        for (var i = 0; i < apiKeyNames.Length && i < createdCustomers.Count; i++)
        {
            await apiKeys.CreateKeyAsync(new CreateCustomerApiKeyDto
            {
                CustomerId         = createdCustomers[i].Id,
                Name               = apiKeyNames[i],
                Scopes             = i % 2 == 0 ? ["read", "write"] : ["read"],
                RateLimitPerMinute = 60 + i * 30,
            });
        }

        // ── Third-party / internal service-to-service OAuth clients ──
        var demoOAuthClients = new (string ClientId, string Name, string Description, List<string> Scopes)[]
        {
            ("billing-service",     "Billing Service",       "Internal billing microservice — client-credentials grant", ["billing:read", "billing:write"]),
            ("partner-mobile-app",  "Partner Mobile App",    "Third-party mobile integration partner",                   ["profile:read"]),
            ("notification-service","Notification Service",  "Internal service that sends email/SMS/push notifications", ["notifications:send"]),
            ("analytics-pipeline",  "Analytics Pipeline",    "Nightly ETL job reading aggregate usage data",             ["analytics:read"]),
            ("mobile-ios-app",      "Mobile App (iOS)",      "First-party iOS app — client-credentials for backend calls", ["profile:read", "profile:write"]),
            ("mobile-android-app",  "Mobile App (Android)",  "First-party Android app — client-credentials for backend calls", ["profile:read", "profile:write"]),
            ("ci-pipeline",         "CI/CD Pipeline",        "Automated deployment pipeline — provisions test tenants",  ["admin:provision"]),
            ("support-portal",      "Support Portal",        "Internal support tooling — read-only customer lookups",    ["customers:read"]),
        };

        foreach (var (clientId, name, description, scopes) in demoOAuthClients)
        {
            await oauthClients.CreateClientAsync(new CreateOAuthClientDto
            {
                ClientId      = clientId,
                Name          = name,
                Description   = description,
                AllowedScopes = scopes,
            });
        }
    }
}
