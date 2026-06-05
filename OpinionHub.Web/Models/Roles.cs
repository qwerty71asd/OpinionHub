namespace OpinionHub.Web.Models;

/// <summary>
/// Имена ролей в Identity. Раньше было три (Participant, Author, Admin),
/// но прав у Participant и Author не отличалось, поэтому их объединили в одну роль User.
/// Миграция старых ролей выполняется в DbInitializer.
/// </summary>
public static class Roles
{
    public const string User = "User";
    public const string Admin = "Admin";

    /// <summary>Все актуальные роли — порядок важен для UI (radio buttons): User первым.</summary>
    public static readonly string[] All = { User, Admin };
}
