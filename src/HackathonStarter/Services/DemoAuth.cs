using System.Security.Claims;
using HackathonStarter.Config;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

namespace HackathonStarter.Services;

/// <summary>
/// Demo users instead of login: real ASP.NET Core cookie authentication, but signing in is one click
/// (no passwords, no sign-up). Roles work everywhere: <c>[Authorize(Roles = ...)]</c>, <c>&lt;AuthorizeView&gt;</c>,
/// and <see cref="CurrentUser.Require"/> in services.
/// Need real auth later? Replace the /switch-user endpoint with your identity provider; the rest stays.
/// </summary>
public static class DemoAuth
{
    public static void AddDemoAuth(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "app_user";
                o.ExpireTimeSpan = TimeSpan.FromDays(30);
                o.LoginPath = "/switch-user/" + SiteConfig.Users[0].Id;
            });
        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<CurrentUser>();
    }

    public static void UseDemoAuth(this WebApplication app)
    {
        app.UseAuthentication();

        // First visit: sign in the default demo user automatically, so there is never a "logged out" state.
        app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated != true
                && !context.Request.Path.StartsWithSegments("/switch-user")
                && !context.Request.Path.StartsWithSegments("/health"))
            {
                var principal = CreatePrincipal(SiteConfig.Users[0]);
                await context.SignInAsync(principal);
                context.User = principal;
            }
            await next();
        });

        app.UseAuthorization();

        // GET /switch-user/{id}?returnUrl=/items → sets the cookie and goes back.
        app.MapGet("/switch-user/{id}", async (HttpContext context, string id, string? returnUrl) =>
        {
            var user = SiteConfig.Users.FirstOrDefault(u => u.Id == id) ?? SiteConfig.Users[0];
            await context.SignInAsync(CreatePrincipal(user));
            return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/') ? "/" : returnUrl);
        });
    }

    private static ClaimsPrincipal CreatePrincipal(DemoUser user) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme));
}

/// <summary>
/// The signed-in demo user, for services, components, API endpoints and background agent runs.
/// In Blazor it reads the circuit's authentication state; elsewhere (API, background jobs, tests) the
/// caller sets the user explicitly with <see cref="Use(DemoUser)"/>.
/// </summary>
public class CurrentUser(IServiceProvider services)
{
    private DemoUser? _user;

    public void Use(DemoUser user) => _user = user;
    public void Use(ClaimsPrincipal principal) => _user = FromPrincipal(principal);

    public async Task<DemoUser> GetAsync()
    {
        if (_user is not null) return _user;
        var auth = services.GetService<AuthenticationStateProvider>();
        if (auth is null) return SiteConfig.Users[0];
        var state = await auth.GetAuthenticationStateAsync();
        return FromPrincipal(state.User);
    }

    /// <summary>Throws if the user doesn't have one of the roles. Use it at the top of mutating service methods.</summary>
    public async Task<DemoUser> Require(params string[] roles)
    {
        var user = await GetAsync();
        if (!roles.Contains(user.Role))
            throw new UnauthorizedAccessException($"Your role ({user.Role}) can't do this.");
        return user;
    }

    public static DemoUser FromPrincipal(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return SiteConfig.Users.FirstOrDefault(u => u.Id == id) ?? SiteConfig.Users[0];
    }
}
