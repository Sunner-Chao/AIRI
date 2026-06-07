namespace Alife.Framework;

public interface IConfigurable
{
    public object? Configuration { get; set; }
}
public interface IConfigurable<T> : IConfigurable where T : class, new()
{
    public new T? Configuration { get; set; }

    object? IConfigurable.Configuration { get => Configuration; set => Configuration = value as T; }
}
