namespace Merchant.Api.Domains.Merchants;

/// <summary>
/// merchantKey aday üreticisi (saf). Gateway'in her merchant'a mint ettiği açık dış kimlik:
/// <c>mk_</c> öneki + 32 hane hex (Guid "N"). URL-güvenli, boşluksuz, gizli DEĞİL.
/// Benzersizlik <b>garantisi</b> handler'daki üret-kontrol döngüsündedir; burada yalnız aday üretilir.
/// </summary>
public static class MerchantKeyGenerator
{
    public static string Generate() => "mk_" + Guid.NewGuid().ToString("N");
}