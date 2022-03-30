namespace NewsDistribution;

/// <summary>
///     Подписчик новостей.
/// </summary>
public class Subscriber : ITarget
{
    private readonly string _email;
    private readonly List<News> _newsList = new();

    /// <summary>
    ///     Конструктор.
    /// </summary>
    /// <param name="email">Личный адрес почты.</param>
    public Subscriber(string email)
    {
        _email = email;
    }

    public void Update(News news)
    {
        _newsList.Add(news);
    }

    public override string ToString()
    {
        return string.Join("\n----\n", _newsList);
    }
}