namespace NewsDistribution;

public class Mail : ISource, ITarget
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
        foreach (var target in _subscribers)
        {
            target.Update(_news.Last());
        }
    }

    public void Update(News news)
    {
        _news.Add(news);
        NotifyTargets();
    }
}