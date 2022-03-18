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
        Console.WriteLine($"New Letter for {_email}:");
        _newsList.Add(news);
        Console.WriteLine(PrintLast() + "\n\n");
    }

    /// <summary>
    ///     Печать самой свежей новости в списке новостей.
    /// </summary>
    /// <returns>Новость</returns>
    public string PrintLast()
    {
        return _newsList.Last().ToString();
    }

    public override string ToString()
    {
        return string.Join("\n----\n", _newsList);
    }
}