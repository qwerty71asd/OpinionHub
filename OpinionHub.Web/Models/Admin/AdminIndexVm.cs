namespace OpinionHub.Web.Models.Admin;

public class AdminIndexVm
{
    public List<AdminUserRowVm> Users { get; set; } = new();
    public string? Query { get; set; }
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
}