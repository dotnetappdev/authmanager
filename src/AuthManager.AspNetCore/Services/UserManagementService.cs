using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthManager.AspNetCore.Services;

internal sealed class UserManagementService<TUser> : IUserManagementService
    where TUser : IdentityUser, new()
{
    private readonly UserManager<TUser> _userManager;
    private readonly ILogger<UserManagementService<TUser>> _logger;

    public UserManagementService(UserManager<TUser> userManager, ILogger<UserManagementService<TUser>> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(UserFilter filter, CancellationToken ct = default)
    {
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.ToLower();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(term)) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        if (filter.IsLockedOut.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            query = filter.IsLockedOut.Value
                ? query.Where(u => u.LockoutEnd.HasValue && u.LockoutEnd.Value > now)
                : query.Where(u => !u.LockoutEnd.HasValue || u.LockoutEnd.Value <= now);
        }

        if (filter.EmailConfirmed.HasValue)
            query = query.Where(u => u.EmailConfirmed == filter.EmailConfirmed.Value);

        var totalCount = await query.CountAsync(ct);

        query = filter.SortDescending
            ? query.OrderByDescending(u => EF.Property<object>(u, filter.SortBy))
            : query.OrderBy(u => EF.Property<object>(u, filter.SortBy));

        var users = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        var dtos = new List<UserDto>();
        foreach (var user in users)
        {
            var dto = await MapToDto(user, ct);
            if (!string.IsNullOrEmpty(filter.Role))
            {
                if (!dto.Roles.Contains(filter.Role, StringComparer.OrdinalIgnoreCase))
                    continue;
            }
            dtos.Add(dto);
        }

        return new PagedResult<UserDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<UserDto?> GetUserByIdAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user is null ? null : await MapToDto(user, ct);
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user is null ? null : await MapToDto(user, ct);
    }

    public async Task<UserDto?> GetUserByUserNameAsync(string userName, CancellationToken ct = default)
    {
        var user = await _userManager.FindByNameAsync(userName);
        return user is null ? null : await MapToDto(user, ct);
    }

    public async Task<(bool Success, string[] Errors)> CreateUserAsync(CreateUserDto dto, CancellationToken ct = default)
    {
        var user = new TUser
        {
            UserName = dto.UserName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            EmailConfirmed = dto.EmailConfirmed
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        foreach (var role in dto.Roles)
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        foreach (var claim in dto.Claims)
        {
            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim(claim.Type, claim.Value));
        }

        _logger.LogInformation("User {UserName} created.", dto.UserName);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> UpdateUserAsync(UpdateUserDto dto, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(dto.Id);
        if (user is null)
            return (false, [$"User {dto.Id} not found."]);

        user.UserName = dto.UserName;
        user.Email = dto.Email;
        user.PhoneNumber = dto.PhoneNumber;
        user.EmailConfirmed = dto.EmailConfirmed;
        user.TwoFactorEnabled = dto.TwoFactorEnabled;
        user.LockoutEnabled = dto.LockoutEnabled;
        user.LockoutEnd = dto.LockoutEnd;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        _logger.LogInformation("User {UserId} updated.", dto.Id);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, [$"User {userId} not found."]);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        _logger.LogInformation("User {UserId} deleted.", userId);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user is null)
            return (false, [$"User {dto.UserId} not found."]);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        _logger.LogInformation("Password reset for user {UserId}.", dto.UserId);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> LockUserAsync(string userId, DateTimeOffset? until = null, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, [$"User {userId} not found."]);

        var end = until ?? DateTimeOffset.UtcNow.AddYears(100);
        var result = await _userManager.SetLockoutEndDateAsync(user, end);

        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        _logger.LogInformation("User {UserId} locked until {Until}.", userId, end);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> UnlockUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, [$"User {userId} not found."]);

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        _logger.LogInformation("User {UserId} unlocked.", userId);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> AssignRoleAsync(string userId, string roleName, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, [$"User {userId} not found."]);

        var result = await _userManager.AddToRoleAsync(user, roleName);
        return result.Succeeded
            ? (true, [])
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> RemoveRoleAsync(string userId, string roleName, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, [$"User {userId} not found."]);

        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        return result.Succeeded
            ? (true, [])
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> AddClaimAsync(string userId, ClaimDto claim, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, [$"User {userId} not found."]);

        var result = await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim(claim.Type, claim.Value));
        return result.Succeeded
            ? (true, [])
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> RemoveClaimAsync(string userId, ClaimDto claim, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, [$"User {userId} not found."]);

        var result = await _userManager.RemoveClaimAsync(user, new System.Security.Claims.Claim(claim.Type, claim.Value));
        return result.Succeeded
            ? (true, [])
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public Task<bool> SendConfirmationEmailAsync(string userId, CancellationToken ct = default)
    {
        // Implementation depends on email service - placeholder
        _logger.LogInformation("Confirmation email requested for user {UserId}.", userId);
        return Task.FromResult(true);
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var users = await _userManager.Users.ToListAsync(ct);

        return new DashboardStats
        {
            TotalUsers = users.Count,
            ActiveUsers = users.Count(u => !u.LockoutEnd.HasValue || u.LockoutEnd.Value <= now),
            LockedOutUsers = users.Count(u => u.LockoutEnd.HasValue && u.LockoutEnd.Value > now),
            UnconfirmedEmailUsers = users.Count(u => !u.EmailConfirmed),
        };
    }

    private async Task<UserDto> MapToDto(TUser user, CancellationToken ct)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd,
            AccessFailedCount = user.AccessFailedCount,
            Roles = [.. roles],
            Claims = claims.Select(c => new ClaimDto(c.Type, c.Value)).ToList()
        };
    }
}
