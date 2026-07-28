using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Storage;

/// <summary>
/// The user/role database for AuthManager's self-contained storage provider
/// (<see cref="Core.Options.AuthManagerStorageProvider.SelfContained"/>) — AuthManager's own
/// database, separate from your app's, so it can run without you having configured ASP.NET
/// Identity yourself. Built on the same battle-tested EF Core Identity store schema
/// (<see cref="IdentityDbContext{TUser,TRole,TKey}"/>) that <c>AddEntityFrameworkStores</c>
/// uses in the default <see cref="Core.Options.AuthManagerStorageProvider.AspNetIdentity"/>
/// mode — only the password hasher differs (see <see cref="SelfContainedPasswordHasher{TUser}"/>).
/// </summary>
public sealed class SelfContainedIdentityDbContext<TUser, TRole> : IdentityDbContext<TUser, TRole, string>
    where TUser : IdentityUser
    where TRole : IdentityRole
{
    public SelfContainedIdentityDbContext(DbContextOptions<SelfContainedIdentityDbContext<TUser, TRole>> options)
        : base(options)
    {
    }
}
