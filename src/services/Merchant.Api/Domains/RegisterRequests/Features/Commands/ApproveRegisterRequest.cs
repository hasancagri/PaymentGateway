using MerchantAggregate = Merchant.Api.Domains.Merchants.Merchant;

namespace Merchant.Api.Domains.RegisterRequests.Features.Commands;

/// <summary>
/// US2 — admin onayı: talep Approved olur, merchant O ANDA <b>Provisioning</b> statüsünde doğar
/// (MerchantKey üretilir ama HİÇBİR yerde görünmez), ActivationTicket üretilir ve descriptor
/// contactEmail'ine aktivasyon linkli mail gider (deterministik; SendEmailRequested publish → Mail.Worker). MerchantProvisioned
/// burada YAYINLANMAZ — aktivasyon redeem'de yayınlanır (D1/D4). Hepsi tek <c>[Transactional]</c> (outbox).
/// </summary>
public static class ApproveRegisterRequest
{
    public record ApproveRegisterRequestCommand(Guid RequestId, string? Note = null);

    public class ApproveRegisterRequestResponse
    {
        public Guid MerchantId { get; set; }
        public Guid RequestId { get; set; }
    }

    [Transactional]
    public class ApproveRegisterRequestCommandHandler
    {
        public async Task<FeatureObjectResultModel<ApproveRegisterRequestResponse>> Handle(
            ApproveRegisterRequestCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            Merchant.Api.Options.Onboarding onboarding,
            CancellationToken ct)
        {
            var request = await session.LoadAsync<RegisterRequest>(cmd.RequestId, ct);
            if (request is null)
                return FeatureObjectResultModel<ApproveRegisterRequestResponse>.NotFound();

            // 1) Merchant Provisioning statüsünde doğar; benzersiz MerchantKey üretilir.
            var merchantKey = await GenerateUniqueMerchantKeyAsync(session, ct);
            var merchantResult = MerchantAggregate.CreateForOnboarding(
                merchantKey, request.LegalName, request.ContactEmail, request.WebhookUrl,
                request.TaxId, request.MerchantMail);
            if (!merchantResult.IsSuccess)
                return FeatureObjectResultModel<ApproveRegisterRequestResponse>.Error(merchantResult.Messages);

            var merchant = merchantResult.Data!;

            // 2) Talebi onayla (Pending değilse RET — idempotent koruma).
            var approve = request.Approve(merchant.Id, cmd.Note);
            if (!approve.IsSuccess)
                return FeatureObjectResultModel<ApproveRegisterRequestResponse>.Error(approve.Messages);

            // 3) Aktivasyon bileti merchant üstünde üretilir (015: ayrı ActivationTicket aggregate yok).
            merchant.IssueActivation(DateTime.UtcNow);

            session.Store(merchant);
            session.Update(request);

            // 4) Aktivasyon maili (deterministik) → contactEmail; token merchant.ActivationToken'dan.
            //    Publish → Mail.Worker (SMTP). [Transactional] outbox: yalnız commit'te gider; kritik link
            //    olduğundan Mail.Worker retry/dead-letter ile teslim güvencesi taşır.
            var link = $"{onboarding.ActivationBaseUrl}?token={merchant.ActivationToken}";
            await bus.PublishAsync(new Shared.IntegrationEvents.SendEmailRequested(
                request.ContactEmail,
                "DropShop hesabınızı etkinleştirin",
                "Başvurunuz onaylandı. Aşağıdaki tek kullanımlık linkten hesabınızı etkinleştirip " +
                $"MerchantKey'inizi (yalnız bir kez gösterilir) alın:\n{link}"));

            return FeatureObjectResultModel<ApproveRegisterRequestResponse>.Ok(new ApproveRegisterRequestResponse
            {
                MerchantId = merchant.Id,
                RequestId = request.Id
            });
        }

        private static async Task<string> GenerateUniqueMerchantKeyAsync(IDocumentSession session, CancellationToken ct)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = MerchantKeyGenerator.Generate();
                var exists = await session.Query<MerchantAggregate>().AnyAsync(m => m.MerchantKey == candidate, ct);
                if (!exists)
                    return candidate;
            }

            return MerchantKeyGenerator.Generate();
        }
    }
}
