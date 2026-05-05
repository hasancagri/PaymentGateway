namespace GarantiService.Credentials;

public class MerchantBankCredential
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public Guid BankId { get; set; }
    public string MerchantCode { get; set; } = default!;
    public string TerminalCode { get; set; } = default!;
    public int Status { get; set; }
}