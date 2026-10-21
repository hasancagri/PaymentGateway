using Payment.Api.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Payment.Api.Domains.PaymentLinks.BankCharge;

/// <summary>
/// Phase-1 mutabakat (İlke VII anti-corruption): bankanın OPERASYON yüzü istemcisi — ödeme yüzünden (`;;`)
/// AYRI yüz (`/api/*` JSON zarf + `SubscriptionKey` header). Gün-sonu mutabakat için KAPALI batch özetini
/// pull eder (banka haber vermez, PG sorar — pull modeli). Wire DTO'ları bu dosyada nested; domain'e sızmaz,
/// normalize `ClosedBatch` listesine çevrilir. SubscriptionKey SIR (secret-store); log/trace/DTO'da görünmez.
/// </summary>
public sealed class BankOperationsClient : ISingletonDependency
{
    private const string BatchSummaryPath = "/api/GetPosBatchSummary";
    private static readonly HttpClient Client = new();
    private readonly InterVposBankOptions _banks;

    public BankOperationsClient(InterVposBankOptions banks) => _banks = banks;

    /// <summary>Bankanın kapalı batch özetini çeker (mutabakat kaynağı). Profil yoksa/SubscriptionKey boşsa
    /// ya da çağrı başarısızsa boş liste (mutabakat o bankayı atlar — fail-safe, çekim etkilenmez).</summary>
    public async Task<IReadOnlyList<ClosedBatch>> GetClosedBatchesAsync(string bankCode, CancellationToken ct)
    {
        var profile = _banks.Banks.FirstOrDefault(b => b.BankCode == bankCode);
        if (profile is null || string.IsNullOrEmpty(profile.SubscriptionKey)) return Array.Empty<ClosedBatch>();

        var request = new OpRequest(null, new List<OpParameter> { new("ShopCode", profile.ShopCode) });
        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, profile.BaseUrl.TrimEnd('/') + BatchSummaryPath)
            {
                Content = JsonContent.Create(request)
            };
            msg.Headers.Add("SubscriptionKey", profile.SubscriptionKey);
            var response = await Client.SendAsync(msg, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return Parse(body);
        }
        catch (Exception)
        {
            return Array.Empty<ClosedBatch>(); // mutabakat sonraki tick'te tekrar dener (dayanıklı Process)
        }
    }

    /// <summary>`{data:{batches:[{batchNo,transactionCount,grossTotal,netTotal}]}}` → normalize (saf, test-first).</summary>
    public static IReadOnlyList<ClosedBatch> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<ClosedBatch>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("batches", out var batches) || batches.ValueKind != JsonValueKind.Array)
            return Array.Empty<ClosedBatch>();

        var list = new List<ClosedBatch>();
        foreach (var b in batches.EnumerateArray())
            list.Add(new ClosedBatch(
                b.TryGetProperty("batchNo", out var bn) ? bn.GetString() ?? string.Empty : string.Empty,
                b.TryGetProperty("transactionCount", out var tc) ? tc.GetInt32() : 0,
                b.TryGetProperty("grossTotal", out var gt) ? gt.GetDecimal() : 0m,
                b.TryGetProperty("netTotal", out var nt) ? nt.GetDecimal() : 0m));
        return list;
    }

    // Operasyon yüzü wire (anti-corruption; slice-nested). İstek zarfı: Header + Parameters[].
    private record OpRequest(object? Header, List<OpParameter> Parameters);
    private record OpParameter(string Name, string Value);
}

/// <summary>Normalize kapalı batch (mutabakat domain görünümü; banka wire değil).</summary>
public sealed record ClosedBatch(string BatchNo, int TransactionCount, decimal GrossTotal, decimal NetTotal);
