using System.Globalization;

namespace Payment.Api.Domains.PaymentLinks.BankCharge;

/// <summary>
/// Phase-1 (İlke VII port/adapter + anti-corruption): gerçek InterVpos banka adapter'ı. Kartı
/// form-urlencoded olarak `{BaseUrl}/mpi/Default.aspx`'e (TxnType=Auth, SecureType=NonSecure) POST eder,
/// `;;` ayraçlı cevabı `BankChargeOutcome`'a normalize eder. Wire alan adları + protokol sabitleri bu
/// adapter'a özel (nested/private); domain sınırını geçmez. PAN tele geçer ama saklanmaz/loglanmaz (İlke VI).
/// Her banka bir InterVposBankProfile ile parametrize; Program.cs profil başına bir örnek kurar.
/// </summary>
public sealed class InterVposBankAdapter : IBankPaymentAdapter, IThreeDCapableAdapter
{
    private const string PaymentPath = "/mpi/Default.aspx";
    private const string TxnTypeAuth = "Auth";
    private const string TxnTypeStatusHistory = "StatusHistory";
    private const string SecureTypeNonSecure = "NonSecure";
    private const string SecureType3DModel = "3DModel";
    private const string TryCurrencyNumeric = "949";

    private static readonly HttpClient Client = new();
    private readonly InterVposBankProfile _profile;

    public InterVposBankAdapter(InterVposBankProfile profile) => _profile = profile;

    public string BankCode => _profile.BankCode;

    public async Task<ResultDomain<BankChargeOutcome>> ChargeAsync(BankChargeRequest request, CancellationToken ct)
    {
        // OrderId deneme başına benzersiz (banka (ShopCode,OrderId) keyler). In-doubt'ta AYNI OrderId
        // sorgulanır (banka bu OrderId'yi çekmiş olabilir — çift çekim emniyeti).
        var orderId = $"{request.OrderRef}-{Guid.NewGuid():N}"[..Math.Min(request.OrderRef.Length + 9, 40)];
        var url = _profile.BaseUrl.TrimEnd('/') + PaymentPath;

        string body;
        try
        {
            using var content = new FormUrlEncodedContent(BuildAuthForm(request, orderId));
            var response = await Client.PostAsync(url, content, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception)
        {
            // Cevap kaybı/ağ hatası = BELİRSİZ (in-doubt): banka çekmiş olabilir. Kör "başarısız" demeden
            // AYNI OrderId'yi durum-sorgula (İlke VII; Ders 16 çift-çekim tuzağı). Çözülürse onay kurtarılır;
            // sorgu da başarısızsa RawCode=INDOUBT + providerRef=OrderId → dayanıklı reconcile Process devralır.
            var result = await QueryStatusAsync(orderId, ct);
            return result;
        }

        return ResultDomain<BankChargeOutcome>.Ok(InterVposResponseMapper.Map(body) with { BankOrderId = orderId });
    }

    public async Task<ResultDomain<BankChargeOutcome>> QueryStatusAsync(string orderId, CancellationToken ct)
    {
        var url = _profile.BaseUrl.TrimEnd('/') + PaymentPath;
        try
        {
            using var content = new FormUrlEncodedContent(BuildStatusForm(orderId));
            var response = await Client.PostAsync(url, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return ResultDomain<BankChargeOutcome>.Ok(InterVposResponseMapper.MapStatus(body) with { BankOrderId = orderId });
        }
        catch (Exception)
        {
            // Durum-sorgu da ulaşamadı → HÂLÂ belirsiz: INDOUBT + OrderId taşı → reconcile Process yeniden dener.
            return ResultDomain<BankChargeOutcome>.Ok(
                BankChargeOutcome.Declined(DeclineClass.TechnicalFailure, "INDOUBT", orderId) with { BankOrderId = orderId });
        }
    }

    // Void (iptal) — orgOrderId. İnterVpos TxnType=Void.
    public async Task<ResultDomain<BankChargeOutcome>> VoidAsync(string bankOrderId, CancellationToken ct)
        => await SendReversalAsync("Void", bankOrderId, null, ct);

    // Refund (iade) — orgOrderId + tutar. İnterVpos TxnType=Refund.
    public async Task<ResultDomain<BankChargeOutcome>> RefundAsync(string bankOrderId, decimal amount, CancellationToken ct)
        => await SendReversalAsync("Refund", bankOrderId, amount, ct);

    private async Task<ResultDomain<BankChargeOutcome>> SendReversalAsync(string txnType, string bankOrderId, decimal? amount, CancellationToken ct)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("ShopCode", _profile.ShopCode), new("UserCode", _profile.UserCode), new("UserPass", _profile.UserPass),
            new("TxnType", txnType), new("OrderId", bankOrderId)
        };
        if (amount is { } a) form.Add(new("PurchAmount", a.ToString(CultureInfo.InvariantCulture)));

        try
        {
            using var content = new FormUrlEncodedContent(form);
            var response = await Client.PostAsync(_profile.BaseUrl.TrimEnd('/') + PaymentPath, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return ResultDomain<BankChargeOutcome>.Ok(InterVposResponseMapper.Map(body) with { BankOrderId = bankOrderId });
        }
        catch (Exception)
        {
            return ResultDomain<BankChargeOutcome>.Ok(BankChargeOutcome.Declined(DeclineClass.TechnicalFailure, "NETWORK"));
        }
    }

    // --- 3D Secure (İlke VI: PAN tarayıcıdan bankaya; PG imzalı formu üretir + callback'i doğrular) ---

    public ThreeDForm Build3DForm(string orderRef, decimal amount, string currency, string okUrl, string failUrl)
    {
        var orderId = $"{orderRef}-{Guid.NewGuid():N}"[..Math.Min(orderRef.Length + 9, 40)];
        var purchAmount = amount.ToString(CultureInfo.InvariantCulture);
        var rnd = Guid.NewGuid().ToString("N");
        const string installment = ""; // tek çekim
        var hash = InterVposHash.ComputeRequestHash(_profile.ShopCode, orderId, purchAmount, okUrl, failUrl,
            TxnTypeAuth, installment, rnd, _profile.SecretKey);

        var fields = new List<KeyValuePair<string, string>>
        {
            new("ShopCode", _profile.ShopCode),
            new("OrderId", orderId),
            new("PurchAmount", purchAmount),
            new("Currency", MapCurrency(currency)),
            new("OkUrl", okUrl),
            new("FailUrl", failUrl),
            new("TxnType", TxnTypeAuth),
            new("SecureType", SecureType3DModel),
            new("InstallmentCount", installment),
            new("Rnd", rnd),
            new("Hash", hash)
            // Pan/Expiry/Cvv2 = tarayıcı ekler (PG sunucusu görmez) — form input, gizli alan DEĞİL.
        };
        return new ThreeDForm(_profile.BaseUrl.TrimEnd('/') + PaymentPath, fields, orderId);
    }

    public bool VerifyCallback(IReadOnlyDictionary<string, string> callback)
    {
        if (!callback.TryGetValue("HASH", out var hash) || string.IsNullOrEmpty(hash)) return false;
        var names = callback.GetValueOrDefault("HASHPARAMS", string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (names.Length == 0) return false;
        var values = names.Select(n => callback.GetValueOrDefault(n, string.Empty));
        return InterVposHash.ComputeCallbackHash(values, _profile.SecretKey) == hash;
    }

    public async Task<ResultDomain<BankChargeOutcome>> Complete3DAsync(string orderId, string md, CancellationToken ct)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            // Tamamlama = asıl çekim: SecureType=NonSecure + MD (ACS'ten gelen) — sim bu ikiliyle completion'a dallanır.
            new("ShopCode", _profile.ShopCode), new("UserCode", _profile.UserCode), new("UserPass", _profile.UserPass),
            new("TxnType", TxnTypeAuth), new("SecureType", SecureTypeNonSecure), new("OrderId", orderId), new("MD", md)
        };
        try
        {
            using var content = new FormUrlEncodedContent(form);
            var response = await Client.PostAsync(_profile.BaseUrl.TrimEnd('/') + PaymentPath, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return ResultDomain<BankChargeOutcome>.Ok(InterVposResponseMapper.Map(body) with { BankOrderId = orderId });
        }
        catch (Exception)
        {
            return await QueryStatusAsync(orderId, ct); // tamamlama cevabı kayıp → in-doubt emniyeti
        }
    }

    private List<KeyValuePair<string, string>> BuildStatusForm(string orderId)
        => new()
        {
            new("ShopCode", _profile.ShopCode),
            new("UserCode", _profile.UserCode),
            new("UserPass", _profile.UserPass),
            new("TxnType", TxnTypeStatusHistory),
            new("OrderId", orderId)
        };

    // Kart + kimlik → InterVpos form alanları (wire). PAN/Cvv tele girer; loglanmaz/saklanmaz (İlke VI).
    private List<KeyValuePair<string, string>> BuildAuthForm(BankChargeRequest r, string orderId)
    {
        return new List<KeyValuePair<string, string>>
        {
            new("ShopCode", _profile.ShopCode),
            new("UserCode", _profile.UserCode),
            new("UserPass", _profile.UserPass),
            new("TxnType", TxnTypeAuth),
            new("SecureType", SecureTypeNonSecure),
            new("Pan", r.Card.Pan),
            new("Expiry", FormatExpiry(r.Card.ExpiryMonth, r.Card.ExpiryYear)),
            new("Cvv2", r.Card.Cvv),
            new("PurchAmount", r.Amount.ToString(CultureInfo.InvariantCulture)),
            new("Currency", MapCurrency(r.Currency)),
            new("OrderId", orderId),
            // 093: taksit. InterVpos tek-çekimde boş, taksitte sayı bekler → 1/0 ise boş, >1 ise N.
            new("InstallmentCount", r.InstallmentCount <= 1 ? string.Empty : r.InstallmentCount.ToString(CultureInfo.InvariantCulture)),
            new("CardType", "0")                     // VISA varsayılan (BIN-şema eşlemesi seam)
        };
    }

    // "12" + "2030"/"30" → "1230" (MMYY).
    private static string FormatExpiry(string month, string year)
    {
        var mm = month.Trim().PadLeft(2, '0');
        var y = year.Trim();
        var yy = y.Length >= 2 ? y[^2..] : y.PadLeft(2, '0');
        return mm + yy;
    }

    private static string MapCurrency(string currency)
        => string.Equals(currency, "TRY", StringComparison.OrdinalIgnoreCase) ? TryCurrencyNumeric : currency;
}

/// <summary>
/// Anti-corruption çevirmen (saf, test-first): InterVpos `;;` ayraçlı cevabı normalize `BankChargeOutcome`'a
/// çevirir. ProcReturnCode="00" onay; diğer kodlar red sınıfına eşlenir (failover taksonomisi). Slice içinde
/// yaşar (pos-master {Banka}PosResponseDataMapper aynası); test edilebilirlik için public static.
/// </summary>
public static class InterVposResponseMapper
{
    public static BankChargeOutcome Map(string body)
    {
        var fields = Parse(body);
        var code = fields.GetValueOrDefault("ProcReturnCode", string.Empty);

        if (code == "00")
            return BankChargeOutcome.Approved(
                fields.GetValueOrDefault("TransId", string.Empty),
                fields.GetValueOrDefault("AuthCode", string.Empty),
                code);

        var declineClass = code switch
        {
            "51" => DeclineClass.IssuerDecline,        // yetersiz bakiye
            "05" => DeclineClass.IssuerDecline,        // onaylanmadı (do not honour)
            "59" => DeclineClass.FraudDecline,         // fraud (asla retry)
            "91" => DeclineClass.TechnicalFailure,     // issuer zaman aşımı → failover
            "99" => DeclineClass.ConfigError,          // kimlik/biçim hatası → failover (yanlış config)
            _ => DeclineClass.IssuerDecline            // bilinmeyen → en güvenli: issuer (failover YOK)
        };

        var providerRef = fields.GetValueOrDefault("TransId");
        return BankChargeOutcome.Declined(declineClass, string.IsNullOrEmpty(code) ? "UNKNOWN" : code, providerRef);
    }

    /// <summary>In-doubt sonrası StatusHistory cevabını çözer: banka işlemi Authorized/Settled gösteriyorsa
    /// ONAY kurtarılır (çift çekim önlendi); bulunamadı/Voided/Declined → çekilmemiş say, TechnicalFailure
    /// (belirsiz-güvenli: retry/failover zararsız). Status cevabında AuthCode olmayabilir (boş geçilir).</summary>
    public static BankChargeOutcome MapStatus(string body)
    {
        var fields = Parse(body);
        var code = fields.GetValueOrDefault("ProcReturnCode", string.Empty);
        var stat = fields.GetValueOrDefault("TxnStat", string.Empty);

        if (code == "00" && (stat == "Authorized" || stat == "Settled"))
            return BankChargeOutcome.Approved(fields.GetValueOrDefault("OrderId", string.Empty), string.Empty, "00");

        // Banka cevap verdi ama işlem yok/void/declined → ÇÖZÜLDÜ, çekilmemiş (reconcile durur; retry güvenli).
        return BankChargeOutcome.Declined(DeclineClass.TechnicalFailure, "NOTCHARGED");
    }

    // `key=value;;key=value` → sözlük (saf).
    private static Dictionary<string, string> Parse(string body)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(body)) return dict;
        foreach (var pair in body.Split(";;", StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0) continue;
            dict[pair[..idx]] = pair[(idx + 1)..];
        }
        return dict;
    }
}
