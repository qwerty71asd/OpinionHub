namespace OpinionHub.Web.Models.Admin;

public enum AdminUserFilter
{
    All,
    Active,
    Banned,
    Deleted,
    Admin
}

public enum AdminUserSort
{
    UserNameAsc,
    UserNameDesc,
    EmailAsc,
    EmailDesc
}

public class AdminIndexVm
{
    public List<AdminUserRowVm> Users { get; set; } = new();
    public string? Query { get; set; }
    public AdminUserFilter Status { get; set; } = AdminUserFilter.All;
    public AdminUserSort Sort { get; set; } = AdminUserSort.UserNameAsc;

    public int CountAll { get; set; }
    public int CountActive { get; set; }
    public int CountBanned { get; set; }
    public int CountDeleted { get; set; }
    public int CountAdmin { get; set; }

    public int PendingReports { get; set; }
    public int PendingAppeals { get; set; }
}

public class AdminUserRowVm
{
    public string Id { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();

    public bool IsAdmin { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEndUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? BanReason { get; set; }
}
