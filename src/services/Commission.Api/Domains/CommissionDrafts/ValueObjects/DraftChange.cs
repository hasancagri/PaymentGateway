namespace Commission.Api.Domains.CommissionDrafts.ValueObjects;

/// <summary>Uygulanan revizyonun tek satırlık diff'i (eski → yeni; agent admin'e yankılar — FR-008).</summary>
public record DraftChange(int RowNo, string BankName, int Installment, decimal OldRate, decimal NewRate);