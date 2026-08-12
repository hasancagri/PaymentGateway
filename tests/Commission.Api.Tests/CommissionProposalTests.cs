namespace Commission.Api.Tests;

/// <summary>019 — teklif durum makinesi: IssueFrom fotoğraf + bilet, Supersede, Accept/Reject bilet kuralları.</summary>
public class CommissionProposalTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    private static CommissionDraft NewDraft()
    {
        var rows = new List<BankGridSourceRow>
        {
            new("0046", "Akbank", Criteria.Create(CardBrand.Visa, CardType.Credit, TransactionRegion.DOMESTIC, 1).Data!, 1.75m),
            new("0046", "Akbank", Criteria.Create(CardBrand.Visa, CardType.Credit, TransactionRegion.DOMESTIC, 6).Data!, 2.00m),
        };
        return CommissionDraft.CreateFromBankGrid(Guid.NewGuid(), rows, 0.5m).Data!;
    }

    private static CommissionProposal NewProposal(CommissionDraft? draft = null) =>
        CommissionProposal.IssueFrom(draft ?? NewDraft(), ttlHours: 72, Now).Data!;

    [Fact]
    public void IssueFrom_fotograf_ve_bilet_uretir()
    {
        var draft = NewDraft();

        var proposal = NewProposal(draft);

        Assert.Equal(draft.Id, proposal.MerchantId);
        Assert.Equal(ProposalStatus.Pending, proposal.Status);
        Assert.Equal(draft.Rows.Count, proposal.Rows.Count);
        Assert.StartsWith("cp_", proposal.DecisionTicket);
        Assert.Equal(Now.AddHours(72), proposal.TicketExpiresAt);
        Assert.Null(proposal.DecidedTime);
    }

    [Fact]
    public void IssueFrom_fotograf_draft_revizesinden_etkilenmez()
    {
        var draft = NewDraft();
        var proposal = NewProposal(draft);
        var before = proposal.Rows.Select(r => r.Rate).ToList();

        draft.Revise([new DraftOperation { Op = "set", Row = 1, Rate = 3.0m }],
            new Dictionary<(string, Criteria), decimal>());

        Assert.Equal(before, proposal.Rows.Select(r => r.Rate).ToList());
    }

    [Fact]
    public void Supersede_Pending_gecersiz_olur_idempotent()
    {
        var proposal = NewProposal();

        Assert.True(proposal.Supersede().IsSuccess);
        Assert.Equal(ProposalStatus.Superseded, proposal.Status);
        Assert.True(proposal.Supersede().IsSuccess); // idempotent
    }

    [Fact]
    public void Supersede_karar_almis_teklif_Error()
    {
        var proposal = NewProposal();
        proposal.Accept(Now);

        Assert.False(proposal.Supersede().IsSuccess);
        Assert.Equal(ProposalStatus.Accepted, proposal.Status);
    }

    [Fact]
    public void Accept_gecerli_bilet_Accepted()
    {
        var proposal = NewProposal();

        var result = proposal.Accept(Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(ProposalStatus.Accepted, proposal.Status);
        Assert.Equal(Now.AddHours(1), proposal.DecidedTime);
    }

    [Fact]
    public void Accept_kullanilmis_bilet_Error_durum_degismez()
    {
        var proposal = NewProposal();
        proposal.Accept(Now);

        var second = proposal.Accept(Now.AddMinutes(5));

        Assert.False(second.IsSuccess);
        Assert.Contains(second.Messages!, m => m.Code == CommissionResourceConstants.PROPOSAL_TICKET_INVALID);
        Assert.Equal(Now, proposal.DecidedTime); // ilk karar korunur
    }

    [Fact]
    public void Accept_TTL_dolmus_Error()
    {
        var proposal = NewProposal();

        var result = proposal.Accept(Now.AddHours(73));

        Assert.False(result.IsSuccess);
        Assert.Equal(ProposalStatus.Pending, proposal.Status);
    }

    [Fact]
    public void Accept_Superseded_Error()
    {
        var proposal = NewProposal();
        proposal.Supersede();

        var result = proposal.Accept(Now);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProposalStatus.Superseded, proposal.Status);
    }

    [Fact]
    public void Reject_gerekce_kaydedilir()
    {
        var proposal = NewProposal();

        var result = proposal.Reject("6 ve 9 taksit oranları yüksek; tek çekim kabul.", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProposalStatus.Rejected, proposal.Status);
        Assert.Equal("6 ve 9 taksit oranları yüksek; tek çekim kabul.", proposal.RejectReason);
        Assert.Equal(Now, proposal.DecidedTime);
    }

    [Fact]
    public void Reject_bos_gerekce_Error()
    {
        var proposal = NewProposal();

        var result = proposal.Reject("   ", Now);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProposalStatus.Pending, proposal.Status);
    }

    [Fact]
    public void Reject_TTL_dolmus_veya_kullanilmis_Error()
    {
        var proposal = NewProposal();

        Assert.False(proposal.Reject("gerekçe", Now.AddHours(73)).IsSuccess);

        proposal.Accept(Now);
        Assert.False(proposal.Reject("gerekçe", Now).IsSuccess);
        Assert.Equal(ProposalStatus.Accepted, proposal.Status);
    }
}