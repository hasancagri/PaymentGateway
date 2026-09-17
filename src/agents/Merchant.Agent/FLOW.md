# Merchant.Agent — Domain Süreci

**BC ne yapar:** BC DEĞİL — A2A host, stateless LLM router. Admin'in metinle verdiği talebi (kayıt
başvurusu / komisyon teklifi) doğru sırayla doğru MCP tool'una yönlendirir; karar/sır/kimlik/oran
ÜRETMEZ, bunlar domain'den (Merchant.Api, Commission.Api) gelir. Kendi DB'si yok.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Açılışta iki MCP uçtan tool keşfi yapılır.** `merchant-api`'nin `/mcp`'si (Bearer, `merchant.write`)
   ve `commission-api`'nin `/mcp`'si sırayla sorgulanır; her uç kendi `McpClient`'ıyla `ListTools`
   çağırır, sonuç `AITool` listesine dönüşüp `ChatClientAgent`'a verilir. Keşif başarısızsa (uç ayakta
   değil/tool yok) o uç için BOŞ liste döner — agent yine ayağa kalkar, hata fırlatmaz
   `(McpToolProvider.DiscoverToolsAsync)`.
2. **Admin A2A üzerinden metin isteği gönderir.** Router (LLM, sıfır domain karar yetkisi) istek
   metnine göre kayıt mı komisyon mu olduğunu ayırt eder; talimatlar `ConstValues.RouterInstructions`'ta
   sabit `(builder.AddA2AServer(agent) → app.MapA2AJsonRpc)`.
3. **Kayıt başvurusu.** "Gateway'e kayıt olmak istiyorum" → `submit_registration` tool'u (Merchant.Api
   `/mcp`) kullanıcının verdiği alanlarla çağrılır; başvuru Pending doğar. Durum sorusu ("başvurum ne
   durumda?") → `registration_status` tool'u e-posta ile çağrılır; Approved yanıtı MerchantId+
   MerchantKey taşır `(SubmitRegistrationMcpTool.SubmitRegistrationAsync / RegistrationStatusMcpTool.RegistrationStatusAsync)`.
4. **Komisyon teklif/pazarlık — TASARIMDA VAR, ÇALIŞMIYOR.** Router talimatları hâlâ `get_merchant` →
   `submit_commission_proposal` / `revise_commission_draft` / `show_commission_draft` /
   `commission_proposal_status` adlı tool'ları çağırmayı öngörür ve Agent Card bu 5 skill'i hâlâ ilan
   eder; ancak bu tool'lar **kod tabanında YOKTUR** — Commission.Api'nin MCP yüzeyi (komisyon taslağı
   domain'i + tool'ları) "022 yapısal eritme" refaktöründe tamamen SÖKÜLDÜ, yerine konmadı. Açılış
   keşfi `commission-api` `/mcp`'sinden BOŞ tool listesi alır (adım 1); LLM bu isimleri çağırmaya
   çalışsa da karşılığı yoktur `(MerchantAgentCard.Create — Skills; ConstValues.RouterInstructions)`.
5. **Yanıt admin'e A2A response olarak döner.** Kayıt/durum akışında gerçek domain sonucu; komisyon
   akışında pratikte "tool bulunamadı" tipi başarısızlık (adım 4 nedeniyle).

## Domain kuralları (süreci yöneten değişmezler)

- **BC değil, stateless.** Kendi persistence'ı yok; her istek MCP tool çağrısıyla ilgili BC'ye gider,
  karar orada verilir.
- **Router domain karar vermez.** Talimatlar açıkça yasaklar: kimlik/sır/merchant anahtarı üretme,
  komisyon oranı öner/hesapla/yuvarlama — bunlar sunucu tarafında (ilgili BC) hesaplanır, LLM yalnız
  admin'in AÇIKÇA verdiği değerleri parametreye taşır.
- **Tool adları dış sözleşmedir.** `submit_registration`/`registration_status` Merchant.Api tarafında
  "değiştirme" notuyla sabitlenmiş (başka tüketiciler de bekliyor); Merchant.Agent bu sözleşmeye bağımlı,
  kendi kopyasını taşımaz.
- **Keşif hatası fail-open.** Bir MCP ucu ayakta değilse/boşsa agent yine de başlar (o uca ait tool'suz);
  crash yerine sessiz-eksik tool seti tercih edilmiş bir tasarım kararı.

## Sınır (bu BC'nin dokunmadığı)

Merchant/RegisterRequest/CommissionPolicy aggregate mantığına dokunmaz — yalnız MCP tool çağrısı yapar.
Kimlik/sır/PAN/merchant anahtarı ÜRETMEZ, prompt'ta da geçirmez (domain'den gelir, aynen yankılanır).

> **GÜNCEL DURUM (kod tabanından doğrulandı, 2026-09-17):** Kayıt akışı (adım 3) CANLI ve çalışır durumda
> — Merchant.Api `/mcp`'de `submit_registration`/`registration_status` gerçekten var. Komisyon
> teklif/pazarlık akışı (adım 4) ÖLÜ — Commission.Api'nin MCP yüzeyi "022" refaktöründe sökülmüş, geri
> kurulmamış; `MerchantAgentCard`/`ConstValues` bu ölü yüzeyi hâlâ ilan/talimat ediyor (drift, temizlenmedi).
> Bu durum README.md'de de not düşülmüş ("skill'ler 022'de ölü, proje derlenir") — güncel ve doğru.
> Admin BFF'in "Agent Chat" ekranı bu agent'ı A2A ile çağırıyor (`src/ui/Admin`, `WithReference(merchantAgent)`);
> dolayısıyla ekrandan komisyon isteği denenirse boş/hatalı tool çağrısıyla karşılaşılır. Payment.Agent
> (ayrı A2A host) de aynı "022'de ölü skill" durumunda ama Merchant.Agent'tan bağımsız, bu dosyanın kapsamı
> dışında.
