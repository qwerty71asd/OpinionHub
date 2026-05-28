using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Services;

public interface ICommentService
{
    /// <summary>
    /// Возвращает дерево комментариев для опроса в виде корней (родительских узлов) с заполненными
    /// <see cref="CommentNodeViewModel.Replies"/>. Сразу проставляет счётчики лайков,
    /// лайкнул ли зритель и лайкнул ли автор опроса.
    /// </summary>
    /// <param name="viewerUserId">null для анонимного зрителя — IsLikedByMe = false для всех узлов.</param>
    Task<IReadOnlyList<CommentNodeViewModel>> GetCommentsTreeAsync(Guid pollId, string? viewerUserId);

    /// <summary>
    /// Создаёт новый комментарий и возвращает готовый узел для рендера на клиенте.
    /// Если есть <paramref name="imagePath"/>, пустой текст допустим. Если ни того, ни другого —
    /// бросает InvalidOperationException. Так же кидает на невалидный pollId/parentCommentId
    /// или parent из другого опроса.
    /// </summary>
    Task<CommentNodeViewModel> CreateAsync(Guid pollId, string authorId, string text, Guid? parentCommentId, string? imagePath = null);
}
