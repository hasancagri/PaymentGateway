namespace Commission.Api.Domains.SharedKernel;

/// <summary>
/// İşlem bölgesi = kartın çıkış menşei (≠ para birimi; TL kısıtıyla çelişmez). Düz enum.
/// </summary>
public enum TransactionRegion
{
    DOMESTIC = 1,
    INTERNATIONAL = 2
}
