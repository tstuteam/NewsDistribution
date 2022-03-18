namespace NewsDistribution;

/// <summary>
///     Новость.
/// </summary>
public record News(string Title, string Description, string Content)
{
    public override string ToString()
    {
        return $"Title: {Title}\nDescription: {Description}\nContent: {Content}";
    }
}