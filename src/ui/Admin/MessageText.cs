using Admin.Clients;

namespace Admin;

/// <summary>API mesaj kodlarını kullanıcıya dönük Türkçe metne çevirir (basit harita).</summary>
public static class MessageText
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["COMMON_MESSAGE_VALUE_IS_REQUIRED"] = "Zorunlu alan boş olamaz",
        ["COMMON_MESSAGE_INVALID_FORMAT"] = "Geçersiz format",
        ["COMMON_MESSAGE_RECORD_NOT_FOUND"] = "Kayıt bulunamadı",
        ["COMMON_MESSAGE_RECORD_DUPLICATE"] = "Bu kayıt zaten var",
        ["COMMON_MESSAGE_INVALID_RANGE"] = "Geçersiz aralık",
        ["COMMON_MESSAGE_INVALID_ENUM_TYPE"] = "Geçersiz değer",
        ["COMMON_MESSAGE_INVALID_VALUE"] = "Geçersiz değer",
        ["COMMON_MESSAGE_SERVER_ERROR"] = "Sunucu hatası",
        ["BANK_HAS_COMMISSIONS"] = "Bankaya bağlı komisyon var, önce onları sil"
    };

    public static string Of(ApiMessage message)
    {
        var text = message.Code is not null && Map.TryGetValue(message.Code, out var mapped)
            ? mapped
            : message.Code ?? "Hata";
        return string.IsNullOrWhiteSpace(message.Property) ? text : $"{message.Property}: {text}";
    }

    public static IEnumerable<string> All(IEnumerable<ApiMessage> messages) => messages.Select(Of);
}