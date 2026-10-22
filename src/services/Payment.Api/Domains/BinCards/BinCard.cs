namespace Payment.Api.Domains.BinCards;

/// <summary>
/// 090 (FR-015/FR-018): BIN→kart bilgisi referans kaydı. Aggregate DEĞİL — salt-okur düz Marten dokümanı
/// (BIN tablosu). Statik embedded JSON'dan startup'ta seed edilir (güncelleme = reseed, dev). Canlı BIN
/// besleme kaynağı sonraki versiyon. Router on-us (issuer == acquirer) + yetenek elemesi (kart tipi) için okur.
/// </summary>
public class BinCard
{
    /// <summary>En uzun olası BIN id uzunluğu (IIN standardı 6→8). PAN prefix lookup bunu AŞMAZ → tam PAN
    /// asla sorgu key'i olmaz (İlke VI, PAN log/trace'e düşmez). Canlı feed daha uzun BIN getirirse artır.</summary>
    public const int MaxBinLength = 8;

    // Marten kimliği (Program.cs'te Identity(x => x.Bin) ile yapılandırılır).
    public string Bin { get; set; } = string.Empty;
    public string IssuerBankCode { get; set; } = string.Empty;
    public string Scheme { get; set; } = string.Empty;   // Visa/Mastercard/Troy
    public string CardType { get; set; } = string.Empty; // credit/debit

    /// <summary>PAN'ı BIN prefix'iyle çözer — en SPESİFİK (en uzun) eşleşen kaydı döndürür; eşleşme yoksa
    /// null (bilinmeyen BIN → routing fail-open, on-us atlanır). Saf fonksiyon (domain-TDD).</summary>
    public static BinCard? ResolveByPan(IReadOnlyList<BinCard> cards, string pan)
    {
        if (string.IsNullOrWhiteSpace(pan)) return null;

        BinCard? best = null;
        foreach (var card in cards)
        {
            if (string.IsNullOrEmpty(card.Bin) || !pan.StartsWith(card.Bin, StringComparison.Ordinal))
                continue;
            if (best is null || card.Bin.Length > best.Bin.Length)
                best = card;
        }
        return best;
    }
}
