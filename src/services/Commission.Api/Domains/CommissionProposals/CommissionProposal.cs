using Commission.Api.Domains.CommissionDrafts;
using Commission.Api.Domains.CommissionDrafts.ValueObjects;

namespace Commission.Api.Domains.CommissionProposals;

/// <summary>
/// Gönderilmiş taslak fotoğrafı + karar bileti (019). Her gönderim YENİ kayıt (Id = teklif kimliği);
/// merchant başına yalnız SON Pending teklif karar alabilir — yeni gönderimde önceki Pending
/// Superseded olur (FR-011). Satırlar gönderim anının kopyasıdır (immutable; banka grid'i sonradan
/// değişse de teklif sabit). Bilet: tek kullanım + TTL; yetkinin kendisidir (FR-004, anonim uçlar).
/// </summary>
public class CommissionProposal : AggregateRoot
{
    private CommissionProposal()
    {
    }

    public Guid MerchantId { get; private set; }

    /// <summary>Gönderim anı fotoğrafı (satır no'lu; Excel ekiyle birebir).</summary>
    public List<DraftRow> Rows { get; private set; } = new();

    public ProposalStatus Status { get; private set; } = ProposalStatus.Pending;

    /// <summary>Tek-kullanımlık karar jetonu (cp_ + Guid "N"); mail linklerinin yol parçası.</summary>
    public string DecisionTicket { get; private set; } = string.Empty;

    public DateTime TicketExpiresAt { get; private set; }

    public DateTime? DecidedTime { get; private set; }

    /// <summary>Ret gerekçesi (serbest metin; uzun itiraz listesi olabilir — FR-006).</summary>
    public string? RejectReason { get; private set; }

    /// <summary>
    /// Taslağın fotoğrafından Pending teklif üretir: satır kopyası + yeni bilet + TTL. Fabrika 014
    /// sözleşmesiyle Ok sarılıdır.
    /// </summary>
    /// <remarks>Handler: SubmitCommissionProposalForAgentCommandHandler</remarks>
    public static ResultDomain<CommissionProposal> IssueFrom(CommissionDraft draft, int ttlHours, DateTime now) =>
        ResultDomain<CommissionProposal>.Ok(new CommissionProposal
        {
            MerchantId = draft.Id,
            Rows = draft.Rows.ToList(),
            Status = ProposalStatus.Pending,
            DecisionTicket = $"cp_{Guid.NewGuid():N}",
            TicketExpiresAt = now.AddHours(ttlHours)
        });

    /// <summary>
    /// Yeni gönderimde önceki Pending teklifi geçersiz kılar (Pending → Superseded; idempotent).
    /// Karar almış (Accepted/Rejected) teklif geçersiz kılınamaz.
    /// </summary>
    /// <remarks>Handler: SubmitCommissionProposalForAgentCommandHandler</remarks>
    public ResultDomain Supersede()
    {
        if (Status == ProposalStatus.Superseded)
            return ResultDomain.Ok();

        if (Status != ProposalStatus.Pending)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Status),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });
        }

        Status = ProposalStatus.Superseded;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Bilet geçerliyse (yalnız Pending + TTL dolmamış) teklifi kabul eder; aksi halde durum
    /// DEĞİŞMEZ ve hata döner (kullanılmış / süresi dolmuş / Superseded → geçersiz bilet).
    /// </summary>
    /// <remarks>Handler: AcceptCommissionProposalCommandHandler</remarks>
    public ResultDomain Accept(DateTime now)
    {
        if (Status != ProposalStatus.Pending || now > TicketExpiresAt)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(DecisionTicket),
                Code = CommissionResourceConstants.PROPOSAL_TICKET_INVALID
            });
        }

        Status = ProposalStatus.Accepted;
        DecidedTime = now;
        UpdatedTime = now;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Bilet geçerliyse teklifi gerekçeyle reddeder (gerekçe zorunlu — FR-006); bilet kuralları
    /// Accept ile aynı. Geçersiz bilette durum değişmez.
    /// </summary>
    /// <remarks>Handler: RejectCommissionProposalCommandHandler</remarks>
    public ResultDomain Reject(string reason, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(RejectReason),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        if (Status != ProposalStatus.Pending || now > TicketExpiresAt)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(DecisionTicket),
                Code = CommissionResourceConstants.PROPOSAL_TICKET_INVALID
            });
        }

        Status = ProposalStatus.Rejected;
        RejectReason = reason.Trim();
        DecidedTime = now;
        UpdatedTime = now;
        return ResultDomain.Ok();
    }
}

/// <summary>Teklif durumu (düz enum — Enumeration kalktı, PG #27).</summary>
public enum ProposalStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3,
    Superseded = 4
}