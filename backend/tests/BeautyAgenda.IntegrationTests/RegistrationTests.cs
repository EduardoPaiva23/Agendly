using System.Net;
using System.Net.Http.Json;
using BeautyAgenda.Application.Catalog;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Customers;
using BeautyAgenda.Application.Professionals;
using BeautyAgenda.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace BeautyAgenda.IntegrationTests;

[Collection(ApiCollection.Name)]
public class RegistrationTests(ApiFactory factory)
{
    [Fact]
    public async Task Service_crud_validation_and_conflicts()
    {
        var admin = await factory.CreateClientAsAsync(ApiFactory.AdminEmail);
        var name = $"Hidratação {Guid.NewGuid():N}"[..30];

        var created = await admin.PostAsJsonAsync("/api/services", new ServiceRequest(name, "desc", 45, 79.90m), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var service = (await created.Content.ReadFromJsonAsync<ServiceDto>(ApiFactory.Json))!;
        Assert.Equal(79.90m, service.Price);

        var duplicate = await admin.PostAsJsonAsync("/api/services", new ServiceRequest(name, null, 45, 10m), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalid = await admin.PostAsJsonAsync("/api/services", new ServiceRequest("", null, 0, -1m), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.Content.ReadFromJsonAsync<ValidationProblemDetails>(ApiFactory.Json);
        Assert.Contains("Name", problem!.Errors.Keys);
        Assert.Contains("DurationMinutes", problem.Errors.Keys);
        Assert.Contains("Price", problem.Errors.Keys);

        var update = await admin.PutAsJsonAsync($"/api/services/{service.Id}",
            new ServiceRequest(name, "nova", 60, 99m, IsActive: false), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/services/999999")).StatusCode);
        var publicServices = await admin.GetFromJsonAsync<List<PublicServiceDto>>("/api/public/services", ApiFactory.Json);
        Assert.DoesNotContain(publicServices!, s => s.Id == service.Id);
    }

    [Fact]
    public async Task Professional_services_must_exist()
    {
        var admin = await factory.CreateClientAsAsync(ApiFactory.AdminEmail);
        var created = await admin.PostAsJsonAsync("/api/professionals", new ProfessionalRequest("Profissional X", "(11) 90000-0000"), ApiFactory.Json);
        var professional = (await created.Content.ReadFromJsonAsync<ProfessionalDto>(ApiFactory.Json))!;
        Assert.Equal("11900000000", professional.Phone);

        var response = await admin.PutAsJsonAsync($"/api/professionals/{professional.Id}/services",
            new SetProfessionalServicesRequest([999999]), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var missing = await admin.PutAsJsonAsync("/api/professionals/999999/services",
            new SetProfessionalServicesRequest([]), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Customer_crud_search_and_unique_phone()
    {
        var admin = await factory.CreateClientAsAsync(ApiFactory.AdminEmail);
        var phone = Scenario.NewPhone();

        var created = await admin.PostAsJsonAsync("/api/customers", new CustomerRequest("Patrícia Rocha", phone, "PAT@Example.com"), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = (await created.Content.ReadFromJsonAsync<CustomerDto>(ApiFactory.Json))!;
        Assert.Equal("pat@example.com", customer.Email);

        var duplicate = await admin.PostAsJsonAsync("/api/customers", new CustomerRequest("Outra", phone, null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalid = await admin.PostAsJsonAsync("/api/customers", new CustomerRequest("", "123", "x"), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var page = await admin.GetFromJsonAsync<PagedResult<CustomerDto>>("/api/customers?search=Patrícia&pageSize=5", ApiFactory.Json);
        Assert.Contains(page!.Items, c => c.Id == customer.Id);
        Assert.Equal(5, page.PageSize);

        var update = await admin.PutAsJsonAsync($"/api/customers/{customer.Id}",
            new CustomerRequest("Patrícia R.", phone, null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/customers/999999")).StatusCode);
    }
}
