namespace Payment.Api.Domains.BinCards;

/// <summary>Kart programı (taksit-banka türetmesinin anahtarı). Değerler CP.VPOS
/// <c>CreditCardProgram</c> ile birebir (parite).</summary>
public enum CardProgram
{
    Unknown = -1,
    Axess = 0,
    Bank24 = 1,
    Bankkart = 2,
    Bonus = 3,
    CardFinans = 4,
    Maximum = 5,
    MilesAndSmiles = 6,
    Neo = 7,
    Paraf = 8,
    ShopAndFly = 9,
    Wings = 10,
    World = 11,
    Advantage = 12,
    SaglamKart = 13
}