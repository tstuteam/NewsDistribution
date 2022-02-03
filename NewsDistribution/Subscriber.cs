namespace NewsDistribution;

public class Subscriber: ITarget
{
    private readonly string _email;
    private readonly List<News> _newsList = new();

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

    public string PrintLast()
    {
        return _newsList.Last().ToString();
    }

    public override string ToString()
    {
        return string.Join("\n----\n", _newsList);
    }
}