using System.Security.Claims;
using SampleApp.WebApi.Models;

namespace SampleApp.WebApi.Endpoints;

/// <summary>
/// Example domain endpoints that demonstrate role-based authorization.
/// Replace with your real business logic.
/// </summary>
public static class ProductEndpoints
{
    private static readonly IReadOnlyList<object> _catalog =
    [
        new { Id = 1, Name = "Basic Widget",   Price = 9.99m,  Premium = false },
        new { Id = 2, Name = "Pro Widget",     Price = 49.99m, Premium = true  },
        new { Id = 3, Name = "Gadget Deluxe",  Price = 99.99m, Premium = true  },
    ];

    public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAll)
            .WithSummary("List all public products")
            .AllowAnonymous();

        group.MapGet("/premium", GetPremium)
            .WithSummary("List premium products — requires the 'Premium' role")
            .RequireAuthorization(p => p.RequireRole("Premium", "Admin", "SuperAdmin"));

        group.MapGet("/admin/report", GetReport)
            .WithSummary("Sales report — requires the 'Admin' or 'SuperAdmin' role")
            .RequireAuthorization(p => p.RequireRole("Admin", "SuperAdmin"));

        return group;
    }

    private static IResult GetAll() =>
        Results.Ok(_catalog.Where(p => !(bool)p.GetType().GetProperty("Premium")!.GetValue(p)!));

    private static IResult GetPremium(ClaimsPrincipal user) =>
        Results.Ok(new
        {
            CallerEmail = user.FindFirstValue(ClaimTypes.Email),
            Products    = _catalog
        });

    private static IResult GetReport(ClaimsPrincipal user) =>
        Results.Ok(new
        {
            ReportDate  = DateTimeOffset.UtcNow,
            GeneratedBy = user.FindFirstValue(ClaimTypes.Email),
            TotalRevenue = _catalog.Sum(p => (decimal)p.GetType().GetProperty("Price")!.GetValue(p)!),
            TotalProducts = _catalog.Count
        });
}
