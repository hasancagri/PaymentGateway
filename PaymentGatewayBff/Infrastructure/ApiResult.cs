namespace PaymentGatewayBff.Infrastructure;

public class ApiResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public List<object>? Messages { get; set; }
}