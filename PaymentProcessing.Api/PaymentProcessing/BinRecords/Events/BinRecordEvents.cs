namespace PaymentProcessing.Api.Modules.PaymentProcessing.BinRecords.Events;

public sealed record BinRecordCreated(
    Guid EventId,
    DateTime OccurredOn,
    Guid BinRecordId,
    string BinStart,
    string BinEnd,
    string CardBrand,
    string CardType
);

public sealed record BinDatabaseImported(
    Guid EventId,
    DateTime OccurredOn,
    int TotalRecords,
    DateTime ImportedAt
);