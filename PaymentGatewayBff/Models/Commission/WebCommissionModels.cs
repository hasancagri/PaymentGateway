namespace PaymentGatewayBff.Models.Commission;

public class WebMerchantCommissionListItem
{
    public Guid Id { get; set; }
    public Guid BankCommissionId { get; set; }
    public decimal Rate { get; set; }
    public int CardBrand { get; set; }
    public int CardType { get; set; }
    public int TransactionRegion { get; set; }
}

public class WebBankCommissionListItem
{
    public Guid Id { get; set; }
    public Guid BankId { get; set; }
    public decimal Rate { get; set; }
    public int CardBrand { get; set; }
    public int CardType { get; set; }
    public int TransactionRegion { get; set; }
}