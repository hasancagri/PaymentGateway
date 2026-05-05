using System.Xml.Serialization;

namespace GarantiService.Models;

[XmlRoot("GVPSRequest")]
public class GarantiAuthRequest
{
    [XmlElement("Mode")] public string Mode { get; set; } = "TEST";
    [XmlElement("Version")] public string Version { get; set; } = "v0.01";
    [XmlElement("Terminal")] public GarantiTerminal Terminal { get; set; } = default!;
    [XmlElement("Customer")] public GarantiCustomer Customer { get; set; } = default!;
    [XmlElement("Card")] public GarantiCard Card { get; set; } = default!;
    [XmlElement("Order")] public GarantiOrder Order { get; set; } = default!;
    [XmlElement("Transaction")] public GarantiTransaction Transaction { get; set; } = default!;
}

public class GarantiTerminal
{
    [XmlElement("ProvUserID")] public string ProvUserId { get; set; } = "PVPOSUSER";
    [XmlElement("HashData")] public string HashData { get; set; } = default!;
    [XmlElement("UserID")] public string UserId { get; set; } = "PVPOSUSER";
    [XmlElement("ID")] public string TerminalId { get; set; } = default!;
    [XmlElement("MerchantID")] public string MerchantId { get; set; } = default!;
}

public class GarantiCustomer
{
    [XmlElement("IPAddress")] public string IpAddress { get; set; } = default!;
}

public class GarantiCard
{
    [XmlElement("Number")] public string Number { get; set; } = default!;
    [XmlElement("ExpireDate")] public string ExpireDate { get; set; } = default!;
    [XmlElement("CVV2")] public string Cvv2 { get; set; } = default!;
}

public class GarantiOrder
{
    [XmlElement("OrderID")] public string OrderId { get; set; } = default!;
}

public class GarantiTransaction
{
    [XmlElement("Type")] public string Type { get; set; } = "sales";
    [XmlElement("InstallmentCnt")] public string InstallmentCnt { get; set; } = default!;
    [XmlElement("Amount")] public string Amount { get; set; } = default!;
    [XmlElement("CurrencyCode")] public string CurrencyCode { get; set; } = default!;
    [XmlElement("CardholderPresentCode")] public string CardholderPresentCode { get; set; } = "0";
    [XmlElement("MotoInd")] public string MotoInd { get; set; } = "N";
}

[XmlRoot("GVPSResponse")]
public class GarantiAuthResponse
{
    [XmlElement("Transaction")] public GarantiTransactionResponse Transaction { get; set; } = default!;
}

public class GarantiTransactionResponse
{
    [XmlElement("Response")] public GarantiResponseDetail Response { get; set; } = default!;
    [XmlElement("RetrefNum")] public string? RetrefNum { get; set; }
    [XmlElement("AuthCode")] public string? AuthCode { get; set; }
    [XmlElement("TxnID")] public string? TxnId { get; set; }
}

public class GarantiResponseDetail
{
    [XmlElement("Code")] public string Code { get; set; } = default!;
    [XmlElement("Message")] public string? Message { get; set; }
    [XmlElement("ErrorMsg")] public string? ErrorMessage { get; set; }
}