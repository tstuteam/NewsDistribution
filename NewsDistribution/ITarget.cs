namespace NewsDistribution;

/// <summary>
///     Приёмник событий.
/// </summary>
public interface ITarget
{
    /// <summary>
    ///     Добавление новости в приёмник.
    /// </summary>
    /// <param name="news">Новая новость.</param>
    public void Update(News news);
}