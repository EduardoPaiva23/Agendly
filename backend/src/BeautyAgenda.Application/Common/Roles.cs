namespace BeautyAgenda.Application.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";

    public static readonly string[] All = [Admin, Staff];
}

public static class Policies
{
    /// <summary>Admin only: catalog, professionals and business hours.</summary>
    public const string AdminOnly = nameof(AdminOnly);
    /// <summary>Admin or Staff: daily operation (appointments, customers, time-offs, dashboard).</summary>
    public const string StaffOrAdmin = nameof(StaffOrAdmin);
}
