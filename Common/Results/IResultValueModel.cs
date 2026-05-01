namespace Common
{
    public interface IResultValueModel<TValue> : IResultModel
    {
        TValue Value { get; set; }
    }
}
