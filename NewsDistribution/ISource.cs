namespace NewsDistribution;

public interface ISource
{
    public void RegisterTarget(ITarget target);

    public void RemoveTarget(ITarget target);

    public void NotifyTargets();
}