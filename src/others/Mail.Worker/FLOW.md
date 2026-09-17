# Mail.Worker — Domain Süreci

**BC ne yapar:** `mail.delivery` fanout kuyruğunu dinler, gelen mesajı domain bilmeden SMTP ile
(Mailpit) gönderir; opsiyonel generic tablo varsa `.xlsx` ekine çevirir. DB'siz worker, MCP değil.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Mail talebi tüketilir.** Mesaj gönderime yetecek her şeyi taşır          `(SendEmailRequested`
   (alıcı, konu, gövde, HTML bayrağı, opsiyonel tablo eki); worker           ` → SendEmailHandler)`
   çağıran BC'ye SORMAZ, içeriği üretmez — yalnız iletir.
2. **Tablo eki varsa `.xlsx`'e çevrilir.** Generic başlık+satır demeti      `(EmailAttachmentTable →`
   domain bilgisi taşımaz; ClosedXML ile sayfa üretilip mesaja eklenir.      ` ClosedXML.XLWorkbook)`
3. **SMTP ile tek seferde gönderilir.** Host/port/kimlik bilgisi Options'tan `(SendEmailHandler`
   okunur (dev = Mailpit); gönderim başarılıysa iz düşülür.                  ` → SmtpClient.SendMailAsync)`
4. **SMTP hatası yutulmadan fırlatılır.** Wolverine artan backoff'la 3 kez  `(opts.Policies.OnException<`
   yeniden dener (1sn/5sn/15sn); tükenirse mesaj kayıp değil, error         ` SmtpException> → MoveToErrorQueue)`
   queue'ya taşınır (elle inceleme).

## Domain kuralları (süreci yöneten değişmezler)

- **DB'siz worker, durumu yok.** Girdi/çıktı yalnız mesaj; kalıcılık (iz, tekrar-deneme kaydı) çağıran
  BC'nin sorumluluğunda değil — burada da tutulmaz, yalnız log satırı düşer.
- **Fat message, sorgu yok.** `SendEmailRequested` gönderime yetecek her alanı taşır (alıcı adresi
  dahil); worker başka servise geri sormaz.
- **İçerik domain-agnostik.** Konu/gövde/HTML metni çağıran BC'de üretilir; worker şablon/persona bilmez
  (NotificationAgent'ın aksine LLM/agent katmanı YOK — düz SMTP transport'u).
- **Outbox garantisi çağıranda.** Yayıncı BC'ler `[Transactional]` handler içinden `PublishMessage`
  kaydıyla yayınlar (bkz. `Merchant.Api/Program.cs`, `Commission.Api/Program.cs`) — yalnız DB commit
  olursa mesaj gider; Mail.Worker bu garantiyi üretmez, yalnız tüketir.

## Sınır (bu BC'nin dokunmadığı)

Mail içeriğinin ne zaman/neden üretileceği çağıran BC'nin kendi sürecidir (`Merchant.Api`,
`Commission.Api` — ikisi de `mail.delivery` exchange'ini yayıncı olarak deklare eder). Şu an bu iki
serviste mesajı fiilen inşa edip yayınlayan aktif bir çağrı noktası yok (yayın kaydı hazır, gönderen
akış henüz/ artık yok — 019'daki komisyon teklif maili akışı sonradan `CommissionPolicies`'e
sadeleşirken düştü); worker bu değişikliğe kör kalır, yalnız kuyruğu dinlemeye devam eder. Ek
depolama/CC/BCC, şablon motoru gibi ihtiyaçlar bu dosyanın kapsamında değil.
