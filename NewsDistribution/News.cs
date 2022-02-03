namespace NewsDistribution;

public class News
{
    private readonly string _title;
    private readonly string _description;
    private readonly string _content;

    public News(string title, string description, string content)
    {
        _title = title;
        _description = description;
        _content = content;
    }

    public override string ToString()
    {
        return $"Title: {_title}\nDescription: {_description}\nContent: {_content}";
    }
}