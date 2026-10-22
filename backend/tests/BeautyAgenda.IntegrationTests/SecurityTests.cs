using System.Net;
using System.Net.Http.Json;
using BeautyAgenda.Application.Auth;
using BeautyAgenda.Application.Catalog;
using BeautyAgenda.IntegrationTests.Infrastructure;

namespace BeautyAgenda.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SecurityTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiFactory.AdminEmail, "wrong-password"), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_user_returns_same_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest("nobody@test.local", ApiFactory.Password), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/customers")]
    [InlineData("/api/appointments")]
    [InlineData("/api/services")]
    [InlineData("/api/professionals")]
    [InlineData("/api/business-hours")]
    [InlineData("/api/dashboard/summary")]
    [InlineData("/api/auth/me")]
    public async Task Admin_endpoints_require_authentication(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_token_is_rejected()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/customers")).StatusCode);
    }

    [Fact]
    public async Task Staff_cannot_manage_catalog_but_can_operate_agenda()
    {
        var staff = await factory.CreateClientAsAsync(ApiFactory.StaffEmail);

        var create = await staff.PostAsJsonAsync("/api/services", new ServiceRequest("Staff service", null, 30, 10m), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        var hours = await staff.PutAsJsonAsync("/api/business-hours", new { items = Array.Empty<object>() }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Forbidden, hours.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/customers")).StatusCode);
    }

    [Fact]
    public async Task Me_returns_user_and_roles()
    {
        var admin = await factory.CreateClientAsAsync(ApiFactory.AdminEmail);
        var me = await admin.GetFromJsonAsync<CurrentUserDto>("/api/auth/me", ApiFactory.Json);

        Assert.Equal(ApiFactory.AdminEmail, me!.Email);
        Assert.Contains("Admin", me.Roles);
    }

    [Fact]
    public async Task Public_endpoints_are_anonymous_and_do_not_expose_contact_data()
    {
        var scenario = await Scenario.CreateAsync(factory);
        var client = factory.CreateClient();

        var professionals = await client.GetStringAsync("/api/public/professionals");
        Assert.DoesNotContain("phone", professionals, StringComparison.OrdinalIgnoreCase);

        var booking = await scenario.BookOkAsync(Scenario.At(Scenario.Tuesday, 10));
        var publicView = await client.GetStringAsync($"/api/public/appointments/{booking.Reference}");
        Assert.DoesNotContain("customer", publicView, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", publicView, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", publicView, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Unknown_or_malformed_reference_returns_404(string reference)
    {
        var response = await factory.CreateClient().GetAsync($"/api/public/appointments/{reference}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_reports_database_ok()
    {
        var response = await factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
