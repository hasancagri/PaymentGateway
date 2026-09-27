# Phase 0 Research: Hassas Hosted Link

Tasarım oturumda kilitlendi (memory: project_sensitive_hosted_link_048). NEEDS CLARIFICATION yok. Kararlar:

## D1 — Erişim modeli: capability-token link (magic-link)

- **Karar**: Süreli + tek-kullanımlık 256-bit token; token URL'de taşınır, sayfa anonim endpoint.
- **Gerekçe**: Ası tehdit LLM'in hassas veriyi görmesi. Token'lı statik sayfa veriyi sunucuda render eder, agent context'ine hiç sokmaz. Link admin'in kendi authenticated oturumunda çıkar (public kanal değil). Kısa TTL + tek-kullanım = endüstri-standart magic-link.
- **Alternatifler**: (a) Platform OIDC login'li sayfa — kullanıcı OTP/login friction istemedi; (b) bearer link'i tam OIDC'ye çevirme — gereksiz ağırlık; (c) Razor Admin projesi (mevcut) — platform-bağımlı değil ama ayrı host + interaktif auth; sökülüyor.

## D2 — Tüketim: GET tüketmez, POST başarıda tüketir

- **Karar**: Görüntüleme (GET) linki tüketmez; başarılı düzenleme (POST) tüketir; TTL backstop.
- **Gerekçe**: Görüntüle→düzenle→kaydet akışı + sayfa yenileme kırılmamalı. ECom 078 D2 aynısı. Katı tek-görüntüleme akışı bozardı.

## D3 — Süre: mutlak son-kullanım, pasif kontrol

- **Karar**: `ExpiresAt = üretim + 15dk` DB'de saklanır; her erişimde `IsUsable` (now <= ExpiresAt && !consumed) bakılır. Timer/job YOK.
- **Gerekçe**: Uygulama restart'ında kaybolmaz; kapalı geçen süre de sayılır; boşta sıfır maliyet.

## D4 — Encoding: dar HTML encoder (& < > " ')

- **Karar**: Enjekte edilen değer yalnız `& < > " '` kaçırır (ECom 078 refactor'undaki dar helper).
- **Gerekçe**: Framework `HtmlEncoder.Default` Türkçe (`ş`→`&#x15F;`) + `+`'yı entity'ye çevirir → ECom byte-eşitliğini bozar; dar set HTML body + tırnaklı attribute'a yeterli+güvenli (`charset=utf-8`). İki repo aynı mantık (kullanıcı kararı). PG'de KRİTİK: merchant adı metakarakter taşıyabilir → her değer encoder'dan + attribute'lar tırnaklı.

## D5 — Markup kaynağı: gömülü .html resource

- **Karar**: HTML `Pages/SensitiveEntry/*.html` dosyalarında, `EmbeddedResource`; C# `GetManifestResourceStream` okur + cache'ler + `{{placeholder}}` doldurur; fragment layout'a RAW.
- **Gerekçe**: Inline C# string yerine gerçek .html (kullanıcı tercihi, IDE renklendirme, escape yok). ECom 078 aynı desene çevrildi (PR #127).

## D6 — Taban adres: config (HttpContext DEĞİL)

- **Karar**: `SensitiveEntryOptions.PublicBaseUrl` config'ten; link `{PublicBaseUrl}/merchants/sensitive/{token}`.
- **Gerekçe**: MCP çağrısı proxy'den (Mcp.Gateway/gateway) gelir; istek base'i iç Aspire adresi, tarayıcıda çözülmez. ECom 078 D1 emsali.

## D7 — Değer gösterimi: TAM (maskesiz)

- **Karar**: Sayfa mevcut değerleri tam gösterir (IBAN/TCKN dahil).
- **Gerekçe**: Düzenleme/doğrulama ekranı — admin yanlışı görüp düzeltmeli. Güvenlik token+süre+tek-kullanımda, maskelemede değil. Maskeleme (IBAN son-4) ileri sertleştirme, kapsam dışı.

## D8 — Yetki: mevcut merchant.admin scope

- **Karar**: Link üretimi `merchant.admin` (mevcut). Token-endpoint anonim (token=capability).
- **Gerekçe**: Yeni scope gereksiz; 043/044 admin scope zaten var. Anonim endpoint İLKE V capability-link istisnası (ECom 078 emsali).
