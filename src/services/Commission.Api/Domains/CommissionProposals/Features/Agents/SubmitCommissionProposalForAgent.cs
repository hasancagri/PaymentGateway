using Commission.Api.Domains.CommissionDrafts;
using Commission.Api.Domains.CommissionDrafts.ValueObjects;

namespace Commission.Api.Domains.CommissionProposals.Features.Agents;

/// <summary>
/// US1/US3 (agent yüzeyi) — teklif sunar / revize sonrası yeniden gönderir ("merchant'a gönder").
/// Agent slice'ları Commands/Queries'e ASLA gitmez; okuma/yazma KENDİ İÇİNDE (015). Akış: değişmezlik
/// bariyeri (Accepted varsa RET — FR-012) → draft yoksa banka grid + marjdan üret (FR-001) → önceki
/// Pending'i Supersede (FR-011) → yeni teklif + bilet → Excel tablolu + 2 linkli mail publish
/// (outbox; yalnız commit'te gider — FR-003). Mail YALNIZ bu slice'tan çıkar (FR-010).
/// </summary>
public static class SubmitCommissionProposalForAgent
{
    public record SubmitCommissionProposalCommand(Guid MerchantId, string MerchantEmail);

    public class SubmitCommissionProposalResponse
    {
        public Guid ProposalId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public bool MailQueued { get; set; }
    }

    [Transactional]
    public class SubmitCommissionProposalForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<SubmitCommissionProposalResponse>> Handle(
            SubmitCommissionProposalCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CommissionProposalOption option,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.MerchantEmail))
            {
                return FeatureObjectResultModel<SubmitCommissionProposalResponse>.Error(new MessageItem
                {
                    Property = "MerchantEmail",
                    Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
                });
            }

            // FR-012 değişmezlik bariyeri #1: Accepted teklif varken yeni teklif RET.
            var hasAccepted = await session.Query<CommissionProposal>()
                .Where(p => p.MerchantId == cmd.MerchantId && p.Status == ProposalStatus.Accepted && !p.IsDeleted)
                .AnyAsync(ct);
            if (hasAccepted)
            {
                return FeatureObjectResultModel<SubmitCommissionProposalResponse>.Error(new MessageItem
                {
                    Property = "Proposal",
                    Code = CommissionResourceConstants.PROPOSAL_ALREADY_ACCEPTED
                });
            }

            var draft = await session.LoadAsync<CommissionDraft>(cmd.MerchantId, ct);

            // Bariyer #2 (çifte): kilitli draft = kabul edilmiş — yeni tur açılamaz.
            if (draft is { IsLocked: true })
            {
                return FeatureObjectResultModel<SubmitCommissionProposalResponse>.Error(new MessageItem
                {
                    Property = "Draft",
                    Code = CommissionResourceConstants.DRAFT_LOCKED
                });
            }

            if (draft is null)
            {
                // FR-001: standart tarife = banka grid'i + sabit marj; ayrı tablo/ekran yok.
                var bankCommissions = await session.Query<BankCommission>()
                    .Where(c => !c.IsDeleted)
                    .ToListAsync(ct);

                var bankNames = (await session.Query<ReferenceBank>().ToListAsync(ct))
                    .ToDictionary(b => b.Code, b => b.Name);

                var sourceRows = bankCommissions
                    .Select(c => new BankGridSourceRow(
                        c.BankCode,
                        bankNames.TryGetValue(c.BankCode, out var name) ? name : c.BankCode,
                        c.Criteria,
                        c.Rate))
                    .ToList();

                var draftResult = CommissionDraft.CreateFromBankGrid(
                    cmd.MerchantId, sourceRows, option.DefaultMarginPoints);
                if (!draftResult.IsSuccess)
                    return FeatureObjectResultModel<SubmitCommissionProposalResponse>.Error(draftResult.Messages);

                draft = draftResult.Data!;
                session.Store(draft);
            }

            // FR-011: önceki Pending teklif(ler) Superseded — yalnız son teklif karar alabilir.
            var pendings = await session.Query<CommissionProposal>()
                .Where(p => p.MerchantId == cmd.MerchantId && p.Status == ProposalStatus.Pending && !p.IsDeleted)
                .ToListAsync(ct);
            foreach (var pending in pendings)
            {
                var supersede = pending.Supersede();
                if (!supersede.IsSuccess)
                    return FeatureObjectResultModel<SubmitCommissionProposalResponse>.Error(supersede.Messages);
                session.Update(pending);
            }

            var proposalResult = CommissionProposal.IssueFrom(draft, option.TicketTtlHours, DateTime.UtcNow);
            var proposal = proposalResult.Data!;
            session.Store(proposal);

            // FR-003: Excel eki (satır no'lu tablo) + kısa özet + mutlak Kabul/Ret linkleri.
            var baseUrl = option.PublicBaseUrl.TrimEnd('/');
            var acceptUrl = $"{baseUrl}/commission-proposals/decision/{proposal.DecisionTicket}/accept";
            var rejectUrl = $"{baseUrl}/commission-proposals/decision/{proposal.DecisionTicket}/reject";

            var attachment = new Shared.IntegrationEvents.EmailAttachmentTable(
                "komisyon-teklifi.xlsx",
                ["Satır No", "Banka", "Kart Markası", "Kart Tipi", "Bölge", "Taksit", "Oran (%)"],
                proposal.Rows.Select(r => new[]
                {
                    r.RowNo.ToString(),
                    r.BankName,
                    r.Criteria.CardBrand.ToString(),
                    r.Criteria.CardType.ToString(),
                    r.Criteria.TransactionRegion.ToString(),
                    r.Criteria.InstallmentCount.ToString(),
                    r.Rate.ToString("0.00")
                }).ToArray());

            var body =
                "Merhaba,\n\n" +
                $"DropShop komisyon teklifiniz hazır ({proposal.Rows.Count} satır). Oran tablosunu ekteki " +
                "Excel dosyasında bulabilirsiniz.\n\n" +
                $"Teklifi KABUL etmek için: {acceptUrl}\n" +
                $"Teklifi gerekçenizle REDDETMEK için: {rejectUrl}\n\n" +
                $"Bu linkler tek kullanımlıktır ve {proposal.TicketExpiresAt:dd.MM.yyyy HH:mm} (UTC) tarihine kadar geçerlidir.\n\n" +
                "DropShop";

            await bus.PublishAsync(new Shared.IntegrationEvents.SendEmailRequested(
                cmd.MerchantEmail,
                "DropShop komisyon teklifiniz",
                body,
                IsHtml: false,
                Attachment: attachment));

            return FeatureObjectResultModel<SubmitCommissionProposalResponse>.Ok(new SubmitCommissionProposalResponse
            {
                ProposalId = proposal.Id,
                Status = proposal.Status.ToString(),
                RowCount = proposal.Rows.Count,
                MailQueued = true
            });
        }
    }
}