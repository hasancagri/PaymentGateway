using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks.Enums;

namespace PaymentGatewayApi.Modules.BankIntegration.MerchantBanks;

public sealed class MerchantBank : AggregateRoot
{
    public Guid MerchantId { get; private set; }
    public Guid BankId { get; private set; }
    public string MerchantCode { get; private set; }
    public string TerminalCode { get; private set; }
    public MerchantBankStatus Status { get; private set; }

    private MerchantBank() { }

    public static ResultDomain<MerchantBank> Assign(
        Guid merchantId, Guid bankId, string merchantCode, string terminalCode)
    {
        var errors = new List<MessageItem>();
        if (merchantId == Guid.Empty)
            errors.Add(new MessageItem { Code = "MerchantBank.MerchantIdEmpty" });
        if (bankId == Guid.Empty)
            errors.Add(new MessageItem { Code = "MerchantBank.BankIdEmpty" });
        if (string.IsNullOrWhiteSpace(merchantCode))
            errors.Add(new MessageItem { Code = "MerchantBank.MerchantCodeEmpty" });
        if (string.IsNullOrWhiteSpace(terminalCode))
            errors.Add(new MessageItem { Code = "MerchantBank.TerminalCodeEmpty" });
        if (errors.Count > 0) return ResultDomain<MerchantBank>.Error(errors);

        return ResultDomain<MerchantBank>.Ok(new MerchantBank
        {
            MerchantId = merchantId,
            BankId = bankId,
            MerchantCode = merchantCode.Trim(),
            TerminalCode = terminalCode.Trim(),
            Status = MerchantBankStatus.Active
        });
    }

    public void Deactivate() => Status = MerchantBankStatus.Passive;
}