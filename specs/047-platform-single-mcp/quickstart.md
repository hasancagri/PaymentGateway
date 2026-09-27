# Quickstart: 047 Canlı Doğrulama

## Ön koşullar

- Üç sistem birlikte ayakta: AgentPlatform (IdP 5001), EC Aspire (fasat + BC'ler), PG Aspire
  (Merchant 5202, Commission 5203, PG IdP 5101). Her biri kendi AppHost'undan.
- AgentPlatform + EC PR'ları merge/checkout edilmiş (PR sırası: platform → EC → PG).
- Claude Desktop config: pg-merchant/pg-commission kayıtları SİLİNMİŞ; tek `store` kaydı
  (fasat `/mcp`). `~/.mcp-auth` cache temizliği: URL-bazlı cache tuzağına dikkat
  (memory `mcp-remote-url-based-cache-gotcha`) — eski token'lar silinir.

## S1 — Admin tek kayıtla PG tool'larını görür + çalıştırır (US1)

1. Claude Desktop → store kaydı → admin kullanıcıyla platform login.
2. `tools/list`: EC admin seti + PG 14 tool'u birlikte (beklenen toplam: EC 48 + PG 14 = 62).
3. `admin_get_merchants` çağır → liste döner. `admin_update_merchant` ile bir alan değiştir →
   başarı + Merchant.Api log'unda scope middleware izi.
4. PASS ölçütü: SC-001 (3 kayıt → 1; işlerin tamamı tek oturumda).

## S2 — Müşteri PG tool'u ne görür ne çağırır (US2)

1. Müşteri rollü kullanıcıyla login (external-customer-agent).
2. `tools/list`: PG tool sayısı 0. Ham `tools/call` ile `admin_get_merchants` dene → yetki hatası
   (PG'de 403/tool-error; işlem yok).
3. PASS ölçütü: SC-002.

## S3 — Kısmi yetki (scope başına budama)

1. Platform rol ekranında test rolü: yalnız `commission.read` (+ müşteri demeti).
2. O rolle login → PG tool'larından YALNIZ `admin_get_commission_policy` görünür; merchant tool'ları
   ve commission yazma tool'ları yok.
3. PASS ölçütü: FR-002 (hep-ya-hiç değil).

## S4 — Yüzey başına tek otorite (FR-005)

1. PG IdP'den `admin-ui` m2m token'ı al (merchant.admin taşır) → `curl -H "Authorization: Bearer ..."
   http://localhost:5202/mcp` (initialize) → 401 BEKLENİR (PG token MCP'ye giremez).
2. Platform token'ıyla Merchant.Api REST admin-düzlem ucu dene → 401 BEKLENİR (ters yön).
3. PASS ölçütü: iki yönde de red.

## S5 — Söküm + regresyon (US3)

1. `external-admin-agent` ile PG IdP `connect/token` → invalid_client (seed silindi + prune).
2. `curl http://localhost:5201/mcp` → 404 (Payment MCP söküldü); PG gateway'de `/mcp/*` rotası 404.
3. Hosted ödeme uçtan uca (EC mağaza → sepet → start_payment → iyzico sandbox → callback → store
   bildirimi → Confirmed) — davranış bugünle birebir (SC-003; 085 T030'da PAS geçilen doğrulama
   burada kapatılır).
4. Onboarding S2S (`ecommerce-onboarding` m2m) + Admin hassas-veri sayfası → PG IdP token'larıyla
   çalışır.

## Regresyon komutları

```bash
# PG
dotnet build && dotnet test
scripts/check-claude-spec-links.sh && scripts/check-flow-links.sh
grep -rn "external-admin-agent\|claude-desktop-pg-mcp" src/ scripts/ 2>/dev/null   # boş beklenir
# EC + AgentPlatform repolarında kendi build+test'leri
```
