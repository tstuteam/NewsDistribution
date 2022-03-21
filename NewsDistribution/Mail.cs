namespace NewsDistribution;

/// <summary>
///     Поставщик новостей.
/// </summary>
public class Mail : ITarget, ISource
{
    private readonly List<News> _news = new();
    private readonly List<ITarget> _subscribers = new();

    public void RegisterTarget(ITarget target)
    {
        _subscribers.Add(target);
    }

    public void RemoveTarget(ITarget target)
    {
        _subscribers.Remove(target);
    }

    public void NotifyTargets()
    {
        _subscribers.ForEach(x => x.Update(_news.Last()));
    }

    public void Update(News news)
    {
        _news.Add(news);
        NotifyTargets();
    }
}