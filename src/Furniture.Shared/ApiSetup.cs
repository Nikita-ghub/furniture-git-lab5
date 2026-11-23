using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Furniture.Shared;

public interface IIdentityVerifier
{
    Task<EmployeeDto?> VerifyAsync(string authorization, CancellationToken cancellationToken);
}

public static class BasicCredentials
{
    public static (string Login, string Password)? Parse(string value)
    {
        if (!AuthenticationHeaderValue.TryParse(value, out var header) ||
            !header.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase) || header.Parameter is null)
            return null;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
            var separator = decoded.IndexOf(':');
            return separator < 1 ? null : (decoded[..separator].ToLowerInvariant(), decoded[(separator + 1)..]);
        }
        catch (FormatException) { return null; }
    }
}

public sealed class BasicAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IIdentityVerifier verifier)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header)) return AuthenticateResult.NoResult();
        if (BasicCredentials.Parse(header) is null) return AuthenticateResult.Fail("Invalid credentials.");
        var employee = await verifier.VerifyAsync(header, Context.RequestAborted);
        if (employee is null || !employee.IsActive) return AuthenticateResult.Fail("Invalid credentials.");
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, employee.Id.ToString()), new(ClaimTypes.Name, employee.Login)
        };
        claims.AddRange(employee.Roles.Select(role => new Claim(ClaimTypes.Role, role.ToString())));
        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"Furniture\", charset=\"UTF-8\"";
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}

public class DependencyUnavailableException : Exception { }

public class ServiceApi(HttpClient http, IHttpContextAccessor accessor)
{
    public async Task<T?> GetAsync<T>(string path, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var auth = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(auth)) request.Headers.Authorization = AuthenticationHeaderValue.Parse(auth);
        using var response = await http.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return default;
        if (!response.IsSuccessStatusCode) throw new DependencyUnavailableException();
        return await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, token);
    }
}

public sealed class RemoteIdentityVerifier(HttpClient http) : IIdentityVerifier
{
    public async Task<EmployeeDto?> VerifyAsync(string authorization, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        if (!response.IsSuccessStatusCode) throw new DependencyUnavailableException();
        return await response.Content.ReadFromJsonAsync<EmployeeDto>(ApiJson.Options, cancellationToken);
    }
}

public static class ApiSetup
{
    public static void AddFurnitureApi(this WebApplicationBuilder builder, bool remoteIdentity = true)
    {
        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false)));
        builder.Services.AddProblemDetails();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAuthentication("Basic").AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>("Basic", _ => { });
        builder.Services.AddAuthorization();
        if (remoteIdentity)
            builder.Services.AddHttpClient<IIdentityVerifier, RemoteIdentityVerifier>(client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["Services:Employees"]!);
                client.Timeout = TimeSpan.FromSeconds(5);
            });
    }

    public static void UseFurnitureApi(this WebApplication app, string service)
    {
        app.Use(async (context, next) =>
        {
            try { await next(context); }
            catch (Exception ex) when (ex is HttpRequestException or DependencyUnavailableException ||
                                       ex is OperationCanceledException && !context.RequestAborted.IsCancellationRequested)
            {
                app.Logger.LogWarning("A required service is unavailable for {Service}.", service);
                await Results.Problem(statusCode: 503, title: "Зависимый сервис недоступен. Повторите запрос позднее.").ExecuteAsync(context);
            }
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/health", () => Results.Ok(new { service, status = "running" })).AllowAnonymous();
        app.MapControllers();
    }
}
