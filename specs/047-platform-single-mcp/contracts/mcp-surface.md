# Kontrat: PG'nin Fasat Downstream Sözleşmesi (047)

Fasat (EC Mcp.Gateway) ile PG BC'leri arasındaki yüzey sözleşmesi. EC 085
`contracts/mcp-surface.md`'nin PG uzantısı — fasat tarafı davranış AYNEN kalır.

## Downstream sözleşmesi (PG'nin karşılaması gerekenler)

1. **Uç**: her PG BC tek `/mcp` (Streamable HTTP, stateless). `pg-merchant` = Merchant.Api 5202,
   `pg-commission` = Commission.Api 5203. Payment.Api MCP ucu YOK (söküldü).
2. **Kimlik**: `/mcp` YALNIZ AgentPlatform-basımlı bearer kabul eder (`"Platform"` şeması;
   issuer `https://localhost:5001`, audience BC'nin kendi adı). PG IdP token'ı → 401.
   Token'sız oturum → 401 (PG'de anonim MCP seti YOK).
3. **Budanmış liste**: `tools/list`, oturum token'ının scope claim'lerine göre BC İÇİNDE süzülür
   (`IsToolVisible`: tool haritada yoksa görünür; varsa gereken scope token'da olmalı). Fasat süzmez.
4. **Son savunma**: budama kaçağında `tools/call` Wolverine `[RequiredScope]` middleware'inde
   fail-closed reddedilir (mevcut 043 mekanizması).
5. **Ad benzersizliği**: PG tool adları `Shared/McpToolNames`'te sabit; EC kümesiyle kesişim 0
   (ölçüldü). Yeni tool eklerken birleşik kümede benzersizlik ön şart; çakışmada PG adı değişir.

## Fasat tarafı (EC — config + sabit listesi)

- `FacadeOption.Downstreams` += pg-merchant/pg-commission (sabit URL — PG ayrı Aspire).
- `FacadeScopes.All` += `merchant.read merchant.write merchant.admin commission.read commission.write`
  (PRM `scopes_supported` ilanı; 079 tuzağı: ilansız scope → talep edilmez → audience eksik → 401).
- `FacadeOption.DiscoveryScope` += aynı demet (m2m tam-katalog registry keşfi).
- Erişilemeyen PG BC'si keşifte atlanır (mevcut graceful-degrade; SC-005 değil FR-008 kapsamı).

## Platform tarafı (AgentPlatform — salt config)

- `AppRegistry.Apps` += `pg` app'i (scope bölmesi + `admin` rol katkısı; data-model.md tablosu).
- `external-admin-agent` (EC app'indeki TEK Claude admin kaydı) Scopes += PG demeti (istemci tavanı).
- `mcp-gateway-discovery` Scopes += PG demeti (keşif token'ı PG audience'larını taşısın).
- DCR şablonu DEĞİŞMEZ → dış DCR istemcisi PG scope'u alamaz (US2/AS-4 teminatı).

## Token akışı (uçtan uca)

```
Claude Desktop ── OAuth (PKCE) ──> AgentPlatform IdP (login + rol∩tavan∩talep)
      │ tek token (scope: EC + PG demetleri, aud: ilgili api'ler)
      ▼
Mcp.Gateway /mcp ── aynı bearer ──> pg-merchant|pg-commission /mcp ("Platform" şeması doğrular)
      │                                   │ scope-budanmış tools/list; tools/call'da [RequiredScope]
      ▼                                   ▼
   tools/list birleşik liste          PG Agents slice handler'ları (değişmez)
```
