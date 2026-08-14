# Contract: ECommerce Tarafı Değişiklikleri (ECommerceWithAgentFramework repo)

**İlke**: tool adları, MCP keşfi, token akışı (`OnboardingGatewayTokenHandler`,
`ecommerce-onboarding` + `merchant.read merchant.write`), `McpClients.MachineOnboarding` — DEĞİŞMEZ.
Değişen yalnız başvuru ALAN SETİ (config + prompt).

## 1. `src/agents/ChatAgent/appsettings.json` — `DropShopGateway:Onboarding` bölümü

Eski alanlar (domain/legalName/taxId/contactEmail/webhookUrl) yerine 023 seti (dev örnek değerleri):

```json
"Onboarding": {
  "Type": "LimitedOrJointStockCompany",
  "Name": "ECommerce Demo",
  "Email": "onboarding@shop.dropshop.local",
  "GsmNumber": "+905551112233",
  "Address": "Demo Mah. E-Ticaret Cad. No:1 İstanbul",
  "Iban": "TR320010009999901234567890",
  "ContactName": "Demo",
  "ContactSurname": "Yönetici",
  "TaxOffice": "Beşiktaş VD",
  "TaxNumber": "1234567890",
  "LegalCompanyTitle": "ECommerce Demo A.Ş."
}
```

(IBAN dev değeri mod-97 GEÇERLİ olmalı — implementasyonda doğrulanıp yazılır.)

## 2. `src/agents/ChatAgent/Program.cs` (~149-156) — prompt alan enjeksiyonu

Yeni Onboarding alanlarını prompt'a gömer (aynı graceful-degrade: config yoksa tool'suz açılır).

## 3. `Prompts.AdminOnboardingInstructions` — persona yönergesi

- Başvuru alanlarını yeni setle sayar; tipe göre koşullu alanları (TCKN/vergi) açıklar.
- Eksik alanı yöneticiden METİNLE ister; uydurma değer üretmez (FR-011).
- Durum sorgusunun `email` ile yapıldığını söyler.
- Approved yanıtındaki MerchantId + MerchantKey'i yöneticiye gösterip Onboarding sayfasındaki
  (033) forma girmesini söyler.
