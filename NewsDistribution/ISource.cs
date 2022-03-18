namespace NewsDistribution;

/// <summary>
///     Источник событий.
/// </summary>
public interface ISource
{
    /// <summary>
    ///     Регистрация приёмника событий.
    /// </summary>
    /// <param name="target">Приёмник событий.</param>
    public void RegisterTarget(ITarget target);

    /// <summary>
    ///     Удаление приёмника событий.
    /// </summary>
    /// <param name="target">Приёмник событий.</param>
    public void RemoveTarget(ITarget target);

    /// <summary>
    ///     Уведомление приёмников об событии.
    /// </summary>
    public void NotifyTargets();
}