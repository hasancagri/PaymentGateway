namespace Payment.Api.Domains.PaymentSessions;

/// <summary>
/// A2A task'ının kalıcı domain izdüşümü: agent-başlatımlı ödeme akışının kimliği + fazı.
/// İki fazlı akış (quote → taksit seçimi) burada state olarak izlenir. Zengin aggregate:
/// private setter + statik <see cref="Create"/> + davranış metotları; invariant'lar burada.
/// 007 terminal fazı = <see cref="PaymentSessionStatus.InstallmentSelected"/> (çekim seam'e devredilir).
/// </summary>
public class PaymentSession : AggregateRoot
{
    private PaymentSession()
    {
    }

    /// <summary>Kayıtlı kart token referansı. PAN DEĞİL — kart verisi burada tutulmaz.</summary>
    public string CardToken { get; private set; } = string.Empty;

    /// <summary>Sepet tutarı (TL). Sunucu-otoriter tutar kaynağı; LLM üretmez.</summary>
    public decimal CartAmount { get; private set; }

    public PaymentSessionStatus Status { get; private set; }

    [JsonProperty("OfferedInstallments")]
    private List<OfferedInstallment> _offeredInstallments = new();

    /// <summary>Sunulan taksit satırlarını salt-okunur döner; iç liste dışarı mutasyona kapalı.</summary>
    /// <remarks>Handler: QuoteInstallmentsForSessionCommandHandler</remarks>
    [JsonIgnore]
    public IReadOnlyList<OfferedInstallment> OfferedInstallments => _offeredInstallments.AsReadOnly();

    /// <summary>Faz 2'de yazılır; ⊂ <see cref="OfferedInstallments"/>.</summary>
    public int? SelectedInstallmentCount { get; private set; }

    /// <summary>Başarısızlık nedeni (kart verisi sızdırmaz).</summary>
    public string? FailReason { get; private set; }

    /// <summary>Yeni oturum açar. <paramref name="cartAmount"/> pozitif olmalı (FR: sepet ≤ 0 reddi).</summary>
    /// <remarks>Handler: QuoteInstallmentsForSessionCommandHandler</remarks>
    public static ResultDomain<PaymentSession> Create(string cardToken, decimal cartAmount)
    {
        if (string.IsNullOrWhiteSpace(cardToken))
        {
            return ResultDomain<PaymentSession>.Error(new MessageItem
            {
                Property = nameof(CardToken),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        if (cartAmount <= 0)
        {
            return ResultDomain<PaymentSession>.Error(new MessageItem
            {
                Property = nameof(CartAmount),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
            });
        }

        return ResultDomain<PaymentSession>.Ok(new PaymentSession
        {
            CardToken = cardToken,
            CartAmount = cartAmount,
            Status = PaymentSessionStatus.Opened
        });
    }

    /// <summary>
    /// Faz 1 sonucu: sunulan taksit listesini oturuma yazar. Status <see cref="PaymentSessionStatus.Opened"/>
    /// olmalı. Boş liste → <see cref="Fail"/> (ödeme alınamıyor). Her satır Model A invariant'ını
    /// (<c>UserTotalAmount == CartAmount</c>) sağlamalı; aksi halde ihlal (Error). Status → QuoteProvided.
    /// </summary>
    /// <remarks>Handler: QuoteInstallmentsForSessionCommandHandler</remarks>
    public ResultDomain OfferInstallments(IEnumerable<OfferedInstallment> installments)
    {
        if (Status != PaymentSessionStatus.Opened)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Status),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });
        }

        var list = installments.ToList();

        if (list.Count == 0)
        {
            Fail("ödeme alınamıyor");
            return ResultDomain.Ok();
        }

        // Model A invariant (CR-1, FR-010): sunulan her satırın kullanıcı tutarı sepet tutarına eşit.
        if (list.Any(o => o.UserTotalAmount != CartAmount))
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(OfferedInstallments),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });
        }

        _offeredInstallments = list;
        Status = PaymentSessionStatus.QuoteProvided;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Faz 2: kullanıcının seçtiği taksiti yazar. Status <see cref="PaymentSessionStatus.QuoteProvided"/>
    /// veya <see cref="PaymentSessionStatus.InstallmentSelected"/> olmalı (tekrar seçim = güncelle).
    /// <paramref name="installmentCount"/> sunulan listede yoksa reddedilir (FR-012). Çekim tetiklenmez.
    /// </summary>
    /// <remarks>Handler: SelectInstallmentCommandHandler</remarks>
    public ResultDomain SelectInstallment(int installmentCount)
    {
        if (Status is not (PaymentSessionStatus.QuoteProvided or PaymentSessionStatus.InstallmentSelected))
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Status),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });
        }

        if (_offeredInstallments.All(o => o.InstallmentCount != installmentCount))
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(SelectedInstallmentCount),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });
        }

        SelectedInstallmentCount = installmentCount;
        Status = PaymentSessionStatus.InstallmentSelected;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Terminal başarısızlık (geçersiz token / POS yok / boş taksit listesi).</summary>
    /// <remarks>Handler: — (çağrılmıyor) — yalnız OfferInstallments içinden</remarks>
    public void Fail(string reason)
    {
        Status = PaymentSessionStatus.Failed;
        FailReason = reason;
        UpdatedTime = DateTime.UtcNow;
    }
}

/// <summary>
/// Kullanıcıya sunulan taksit satırı. Arka plandaki POS/banka TAŞINMAZ (kullanıcı görmez, SC-004).
/// Model A: <see cref="UserTotalAmount"/> = sepet tutarı; <see cref="MonthlyAmount"/> = tutar / taksit.
/// </summary>
public record OfferedInstallment(int InstallmentCount, decimal UserTotalAmount, decimal MonthlyAmount);