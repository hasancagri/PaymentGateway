namespace Commission.Api.Domains.CommissionDrafts.ValueObjects;

/// <summary>
/// Taslak üretiminin girdisi: banka grid'inin tek satırı (banka + kombinasyon + BANKA oranı).
/// Handler <c>BankCommission</c> + yerel banka adı read-model'inden kurar; aggregate'ler arası
/// doğrudan referans taşınmaz.
/// </summary>
public record BankGridSourceRow(string BankCode, string BankName, Criteria Criteria, decimal BankRate);