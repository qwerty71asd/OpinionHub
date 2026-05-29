namespace OpinionHub.Web.Services;

/// <summary>
/// Пороги, при достижении которых отправляется Telegram-уведомление автору опроса/коммента.
/// Срабатывает только при точном попадании в порог (count == 10, 100, 1000) —
/// не каждый лайк. Это решает проблему спама и при этом даёт автору видеть рост популярности.
/// При unlike → like обратно на ту же отметку уведомление повторится; принимаем как acceptable.
/// </summary>
public static class LikeMilestones
{
    private static readonly HashSet<int> Levels = new() { 10, 100, 1000 };

    public static bool IsMilestone(int count) => Levels.Contains(count);
}
