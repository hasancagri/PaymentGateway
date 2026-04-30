namespace PaymentGatewayApi.Modules.BankIntegration.BinRecords.Events;

public sealed record BinRecordCreated(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     BinRecordId,
    string   BinStart,
    string   BinEnd,
    string   CardBrand,
    string   CardType
) : IDomainEvent;

public sealed record BinDatabaseImported(
    Guid     EventId,
    DateTime OccuredOn,
    int      TotalRecords,
    DateTime ImportedAt
) : IDomainEvent;
