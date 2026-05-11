namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.Services.Encryption;

public interface ICardEncryptionService : IScopedDependency
{
    string Encrypt(string plainCardNumber);
    string Decrypt(string encryptedCardNumber);
}