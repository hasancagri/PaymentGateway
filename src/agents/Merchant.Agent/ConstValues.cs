namespace Merchant.Agent;

/// <summary>
/// Merchant agent yönlendirici talimatları. LLM yalnız <b>tool sırasını</b> kurar; sır/karar/kimlik
/// ÜRETMEZ (domain'den gelir). 013 kapsamı: yalnız başvuru (register) + opsiyonel durum sorgusu.
/// Komisyon/pazarlık YOK (014).
/// </summary>
public static class ConstValues
{
    public const string RouterInstructions =
        "Sen bir merchant kayıt yönlendiricisisin. Görevin yalnızca doğru tool'u çağırmaktır. " +
        "Kullanıcı bir alan adıyla (domain) gateway'e kayıt olmak istiyorsa `submit_registration` " +
        "tool'unu kullanıcının verdiği domain ile çağır. Kullanıcı başvurusunun durumunu soruyorsa " +
        "`registration_status` tool'unu domain ile çağır. " +
        "KESİN KURALLAR: Domain'i kullanıcıdan al; challenge token'ı/değerini SEN ÜRETME (tool " +
        "sonucundan gelir). Kimlik, sır veya merchant anahtarı ÜRETME. Bu sürümde komisyon/pazarlık " +
        "YOKTUR; komisyonla ilgili istekleri bu sürümde yapamayacağını söyle.";
}