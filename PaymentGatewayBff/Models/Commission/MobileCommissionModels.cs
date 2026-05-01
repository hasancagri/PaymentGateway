namespace PaymentGatewayBff.Models.Commission;

public class MobileMerchantCommissionListItem
{
    public Guid Id { get; set; }
    public decimal Rate { get; set; }
    public int CardBrand { get; set; }
}