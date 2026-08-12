using Commission.Api.Domains.CommissionDrafts.ValueObjects;

namespace Commission.Api.Domains.CommissionDrafts;

/// <summary>
/// Merchant başına TEK komisyon çalışma kopyası (019). Id = MerchantId (birebir). Satırlar
/// deterministik sıralı (BankCode ASC → Taksit ASC → kart ekseni) ve 1-tabanlı satır numaralı —
/// Excel'deki "satır 37" adreslemesi birebir bu numaradır (FR-014). Revizyonlar taslağı anında
/// değiştirir ama merchant'a hiçbir şey gitmez (FR-010); "gönder" fotoğrafı CommissionProposal yapar.
/// Kabul anında kilitlenir (IsLocked) — hiçbir revizyon kabul edilmez (FR-012).
/// </summary>
public class CommissionDraft : AggregateRoot
{
    private CommissionDraft()
    {
    }

    /// <summary>Deterministik sıralı, satır numaralı taslak satırları.</summary>
    public List<DraftRow> Rows { get; private set; } = new();

    /// <summary>Kabul sonrası true — taslak değişmez (FR-012).</summary>
    public bool IsLocked { get; private set; }

    /// <summary>
    /// Banka grid satırlarından taslağı üretir: her satır = banka oranı + sabit marj; sıralama
    /// deterministik (BankCode ASC → Taksit ASC → CardBrand → CardType → Bölge), RowNo 1-tabanlı.
    /// Boş banka grid'i → RECORD_NOT_FOUND (teklif türetilecek kombinasyon yok).
    /// </summary>
    /// <remarks>Handler: SubmitCommissionProposalForAgentCommandHandler</remarks>
    public static ResultDomain<CommissionDraft> CreateFromBankGrid(
        Guid merchantId, IReadOnlyList<BankGridSourceRow> bankRows, decimal marginPoints)
    {
        if (merchantId == Guid.Empty)
        {
            return ResultDomain<CommissionDraft>.Error(new MessageItem
            {
                Property = "MerchantId",
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        if (bankRows is null || bankRows.Count == 0)
        {
            return ResultDomain<CommissionDraft>.Error(new MessageItem
            {
                Property = "BankGrid",
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });
        }

        var ordered = bankRows
            .OrderBy(r => r.BankCode, StringComparer.Ordinal)
            .ThenBy(r => r.Criteria.InstallmentCount)
            .ThenBy(r => r.Criteria.CardBrand)
            .ThenBy(r => r.Criteria.CardType)
            .ThenBy(r => r.Criteria.TransactionRegion)
            .ToList();

        var rows = ordered
            .Select((r, i) => DraftRow.Create(i + 1, r.BankCode, r.BankName, r.Criteria, r.BankRate + marginPoints))
            .ToList();

        return ResultDomain<CommissionDraft>.Ok(new CommissionDraft
        {
            Id = merchantId,
            Rows = rows,
            IsLocked = false
        });
    }

    /// <summary>
    /// Yapılandırılmış revizyon işlemlerini (set / delta; satır-no, banka+taksit veya filtre adresli)
    /// uygular. Hesap tamamen sunucuda (FR-007). Taban bekçisi: sonuç-oran ilgili BANKA oranının
    /// altına inen TEK satır bile varsa işlem BÜTÜN reddedilir, ihlal satırları listelenir (FR-009);
    /// taslak değişmez. Geçersiz adres (satır yok / kombinasyon yok) → hata. Kilitliyse hata.
    /// Başarıda uygulanan diff listesi döner (FR-008).
    /// </summary>
    /// <remarks>Handler: ReviseCommissionDraftForAgentCommandHandler</remarks>
    public ResultDomain<List<DraftChange>> Revise(
        IReadOnlyList<DraftOperation> operations,
        IReadOnlyDictionary<(string BankCode, Criteria Criteria), decimal> bankFloorLookup)
    {
        if (IsLocked)
        {
            return ResultDomain<List<DraftChange>>.Error(new MessageItem
            {
                Property = "Draft",
                Code = CommissionResourceConstants.DRAFT_LOCKED
            });
        }

        if (operations is null || operations.Count == 0)
        {
            return ResultDomain<List<DraftChange>>.Error(new MessageItem
            {
                Property = "Operations",
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        // Çalışma kopyası: RowNo → güncel oran. İşlemler sıralı uygulanır; taslak ancak TÜM
        // işlemler + taban bekçisi geçerse mutasyona uğrar (bütün-veya-hiç).
        var working = Rows.ToDictionary(r => r.RowNo, r => r.Rate);

        foreach (var op in operations)
        {
            var kind = op.Op?.Trim().ToLowerInvariant();
            if (kind is not ("set" or "delta"))
            {
                return ResultDomain<List<DraftChange>>.Error(new MessageItem
                {
                    Property = "Op",
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE,
                    Params = [op.Op ?? "(boş)"]
                });
            }

            if (kind == "set" && op.Rate is null)
            {
                return ResultDomain<List<DraftChange>>.Error(new MessageItem
                {
                    Property = "Rate",
                    Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
                });
            }

            if (kind == "delta" && op.Delta is null)
            {
                return ResultDomain<List<DraftChange>>.Error(new MessageItem
                {
                    Property = "Delta",
                    Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
                });
            }

            // Adres çözümü — üç biçimden biri zorunlu (inline; 015 private-helper yasağı).
            List<DraftRow> targets;
            if (op.Row is { } rowNo)
            {
                var row = Rows.FirstOrDefault(r => r.RowNo == rowNo);
                if (row is null)
                {
                    return ResultDomain<List<DraftChange>>.Error(new MessageItem
                    {
                        Property = "Row",
                        Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND,
                        Params = [rowNo.ToString(), $"1-{Rows.Count}"]
                    });
                }

                targets = [row];
            }
            else if (!string.IsNullOrWhiteSpace(op.Bank) && op.Installment is { } installment)
            {
                targets = Rows.Where(r =>
                        (string.Equals(r.BankName, op.Bank, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(r.BankCode, op.Bank, StringComparison.OrdinalIgnoreCase)) &&
                        r.Criteria.InstallmentCount == installment)
                    .ToList();
                if (targets.Count == 0)
                {
                    return ResultDomain<List<DraftChange>>.Error(new MessageItem
                    {
                        Property = "Bank/Installment",
                        Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND,
                        Params = [op.Bank!, installment.ToString()]
                    });
                }
            }
            else if (op.Filter is { } filter && (!string.IsNullOrWhiteSpace(filter.Bank) || filter.Installment is not null))
            {
                targets = Rows.Where(r =>
                        (string.IsNullOrWhiteSpace(filter.Bank) ||
                         string.Equals(r.BankName, filter.Bank, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(r.BankCode, filter.Bank, StringComparison.OrdinalIgnoreCase)) &&
                        (filter.Installment is null || r.Criteria.InstallmentCount == filter.Installment))
                    .ToList();
                if (targets.Count == 0)
                {
                    return ResultDomain<List<DraftChange>>.Error(new MessageItem
                    {
                        Property = "Filter",
                        Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND,
                        Params = [filter.Bank ?? "*", filter.Installment?.ToString() ?? "*"]
                    });
                }
            }
            else
            {
                return ResultDomain<List<DraftChange>>.Error(new MessageItem
                {
                    Property = "Address",
                    Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
                });
            }

            foreach (var target in targets)
                working[target.RowNo] = kind == "set" ? op.Rate!.Value : working[target.RowNo] + op.Delta!.Value;
        }

        // Taban bekçisi + sanity: nihai durum üzerinden; TEK ihlal bile hepsini düşürür (FR-009).
        var violations = new List<MessageItem>();
        foreach (var row in Rows)
        {
            var newRate = working[row.RowNo];
            if (newRate == row.Rate)
                continue;

            if (newRate <= 0)
            {
                violations.Add(new MessageItem
                {
                    Property = $"Row:{row.RowNo}",
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE,
                    Params = [row.RowNo.ToString(), row.BankName, row.Criteria.InstallmentCount.ToString(), newRate.ToString("0.####")]
                });
                continue;
            }

            if (bankFloorLookup is not null &&
                bankFloorLookup.TryGetValue((row.BankCode, row.Criteria), out var floor) &&
                newRate < floor)
            {
                violations.Add(new MessageItem
                {
                    Property = $"Row:{row.RowNo}",
                    Code = CommissionResourceConstants.RATE_BELOW_BANK_FLOOR,
                    Params = [row.RowNo.ToString(), row.BankName, row.Criteria.InstallmentCount.ToString(), newRate.ToString("0.####"), floor.ToString("0.####")]
                });
            }
        }

        if (violations.Count > 0)
            return ResultDomain<List<DraftChange>>.Error(violations);

        var changes = new List<DraftChange>();
        Rows = Rows.Select(r =>
        {
            var newRate = working[r.RowNo];
            if (newRate == r.Rate)
                return r;

            changes.Add(new DraftChange(r.RowNo, r.BankName, r.Criteria.InstallmentCount, r.Rate, newRate));
            return r.WithRate(newRate);
        }).ToList();
        UpdatedTime = DateTime.UtcNow;

        return ResultDomain<List<DraftChange>>.Ok(changes);
    }

    /// <summary>Kabul anında taslağı kilitler (idempotent) — sonrası hiçbir revizyon kabul edilmez.</summary>
    /// <remarks>Handler: AcceptCommissionProposalCommandHandler</remarks>
    public ResultDomain Lock()
    {
        if (IsLocked)
            return ResultDomain.Ok();

        IsLocked = true;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}