# Quickstart: Merchant + Commission MCP Yüzeyi Doğrulama

Bu doküman implementasyon sonrası canlı doğrulama akışıdır (uçtan uca senaryo). Tool
sözleşmeleri için `contracts/`, alan detayları için `data-model.md`.

## Ön koşul

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

Aspire dashboard'dan `Merchant.Api`, `Commission.Api`, `Identity.Server` sağlıklı; Mailpit ayakta
(mail bildirimi için). Claude desktop, `external-admin-agent` OAuth client'ıyla (042'den kurulu)
Merchant.Api `/mcp` ve Commission.Api `/mcp` uçlarına bağlı olmalı.

## Senaryo 1 — Başvuru → admin mail → onay (US2 + FR-011)

1. Claude desktop'tan `submit_registration` çağrılır (test verisiyle, US1'deki alan seti).
2. **Beklenen**: yanıt Pending + requestId; Mailpit'te **yalnız admin adresine** "PG'ye kayıt
   yaptırmak isteyen var" tarzı mail görünür (SC-006). Başvurana (merchant adayı) HİÇBİR mail
   gitmez — bu feature kapsamında yalnız admin bildirimi var (bilinçli sınır).
3. Aynı e-postayla `submit_registration` TEKRAR çağrılır.
4. **Beklenen**: yeni kayıt açılmaz, Mailpit'te YENİ mail YOK, dönen mesaj "talepte
   bulunmuştunuz, onayı bekleniyor" (FR-012).
5. `admin_get_pending_registrations` çağrılır → adım 1'deki talep listede görünür.
6. `admin_approve_registration(requestId)` çağrılır → `Approved` + yeni `merchantId` döner.
7. Aynı `requestId` ile tekrar `admin_approve_registration` çağrılır → `INVALID_OPERATION_ERROR`
   (terminal statü korunur, US2 AS4).

## Senaryo 2 — Merchant statü yönetimi (US1)

1. Adım 6'daki `merchantId` ile `admin_get_merchants` çağrılır (filtresiz) → yeni merchant
   `Status: Active` olarak listede görünür (mevcut davranış, yeni merchant Active doğar).
2. `admin_deactivate_merchant(merchantId)` çağrılır → `Status: Passive`, `changed: true`.
3. `admin_activate_merchant(merchantId)` tekrar çağrılır → `Status: Active`, `changed: true`.
4. `admin_activate_merchant(merchantId)` AYNI merchantId ile TEKRAR çağrılır → `changed: false`,
   hata DÖNMEZ (idempotent no-op, US1 AS2).
5. `admin_suspend_merchant(merchantId)` çağrılır → `Status: Suspended`. `admin_get_merchants
   (status: "Suspended")` ile filtrelenmiş listede doğrulanır.

## Senaryo 3 — Komisyon sorgulama (US4)

1. Commission.Api Admin ekranından (CommissionPolicies/Index — DOKUNULMADI) test merchant'ı için
   bir politika tanımlanır (ör. %2.5 + 0.10 TL).
2. Claude desktop'tan `admin_get_commission_policy(merchantId)` çağrılır → tanımlanan marj/tarife
   doğal dille döner.
3. Politikası olmayan başka bir merchantId ile aynı tool çağrılır → "tanımlı politika yok".

## Senaryo 4 — Söküm doğrulaması (FR-004/FR-005/FR-006, SC-003/SC-004)

1. Admin BFF'de `/AgentChat` ve `/RegisterRequests` route'larına gidilir → 404 (sayfalar silindi).
2. `src/agents/Merchant.Agent` projesi çözümde/Aspire AppHost'ta artık YOK (build/dashboard'da
   görünmez).
3. `dotnet build` ve `dotnet test` sıfır hata/başarısız test ile geçer (regresyon yok, SC-005).

## Senaryo 5 — Yetki ayrımı (FR-007, research.md #9 — asıl güvenlik testi)

1. `ecommerce-onboarding` client_credentials ile token alınır (yalnız `merchant.read`+
   `merchant.write` taşır, `merchant.admin` YOK).
2. Bu token'la `admin_activate_merchant` çağrılır → **`401/403`** (`ScopeAuthorizationMiddleware`
   `merchant.admin` eksikliğini yakalar) — statü DEĞİŞMEZ. Bu senaryo geçmezse tasarımın asıl
   amacı (admin/sistem-istemci ayrımı) sağlanmamış demektir.
3. Aynı token'la `submit_registration` çağrılır → **başarılı** (bu tool'un scope'u DEĞİŞMEDİ,
   hâlâ yalnız `merchant.write`).
4. `external-admin-agent` (Claude Desktop) token'ıyla `admin_activate_merchant` çağrılır →
   başarılı (bu client `merchant.admin` taşır).
5. Geçersiz/scope'suz token'la Commission.Api `/mcp`'ye `admin_get_commission_policy` çağrılır →
   `401/403`.

## Doğrulama checklist'i (spec Success Criteria ile eşleme)

- [ ] SC-001 — Senaryo 2
- [ ] SC-002 — Senaryo 2 (adım 1) + Senaryo 3
- [ ] SC-003 — Senaryo 4 (adım 1, RegisterRequests)
- [ ] SC-004 — Senaryo 4 (adım 1, AgentChat)
- [ ] SC-005 — Senaryo 1 + Senaryo 4 (adım 3)
- [ ] SC-006 — Senaryo 1 (adım 2)
- [ ] Ek (plan-level) — Senaryo 5: admin/sistem-istemci ayrımı fiilen çalışıyor