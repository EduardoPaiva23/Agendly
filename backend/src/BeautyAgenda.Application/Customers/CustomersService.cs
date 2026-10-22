using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Scheduling;
using BeautyAgenda.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BeautyAgenda.Application.Customers;

public sealed record CustomerDto(int Id, string Name, string Phone, string? Email, DateTimeOffset CreatedAt);

public sealed record CustomerRequest(string Name, string Phone, string? Email);

public sealed class CustomerRequestValidator : AbstractValidator<CustomerRequest>
{
    public CustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Phone).Must(PhoneNumber.IsValid)
            .WithMessage($"Phone must have between {PhoneNumber.MinDigits} and {PhoneNumber.MaxDigits} digits (with area code).");
        RuleFor(x => x.Email).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class CustomersService(
    IApplicationDbContext db,
    IValidator<CustomerRequest> validator,
    SalonClock clock,
    AuditWriter audit)
{
    public async Task<PagedResult<CustomerDto>> SearchAsync(string? search, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, pageSize);
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var digits = PhoneNumber.Normalize(term);
            query = query.Where(c => c.Name.Contains(term)
                                     || (c.Email != null && c.Email.Contains(term))
                                     || (digits.Length >= 3 && c.Phone.Contains(digits)));
        }

        var (items, total) = await query.OrderBy(c => c.Name).ThenBy(c => c.Id).PageAsync(p, size, ct);
        return new PagedResult<CustomerDto>(items.Select(ToDto).ToList(), p, size, total);
    }

    public async Task<CustomerDto> GetAsync(int id, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("Customer", id);
        return ToDto(customer);
    }

    public async Task<CustomerDto> CreateAsync(CustomerRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var phone = PhoneNumber.Normalize(request.Phone);
        await EnsurePhoneIsFreeAsync(phone, null, ct);

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Phone = phone,
            Email = NormalizeEmail(request.Email),
            CreatedAt = clock.UtcNow,
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        audit.Add(nameof(Customer), customer.Id, "Created");
        await db.SaveChangesAsync(ct);
        return ToDto(customer);
    }

    public async Task<CustomerDto> UpdateAsync(int id, CustomerRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("Customer", id);

        var phone = PhoneNumber.Normalize(request.Phone);
        await EnsurePhoneIsFreeAsync(phone, id, ct);

        customer.Name = request.Name.Trim();
        customer.Phone = phone;
        customer.Email = NormalizeEmail(request.Email);
        audit.Add(nameof(Customer), customer.Id, "Updated");
        await db.SaveChangesAsync(ct);
        return ToDto(customer);
    }

    private async Task EnsurePhoneIsFreeAsync(string phone, int? exceptId, CancellationToken ct)
    {
        if (await db.Customers.AnyAsync(c => c.Phone == phone && c.Id != exceptId, ct))
            throw new ConflictException("Another customer already uses this phone number.");
    }

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private CustomerDto ToDto(Customer c) => new(c.Id, c.Name, c.Phone, c.Email, clock.ToLocal(c.CreatedAt));
}
