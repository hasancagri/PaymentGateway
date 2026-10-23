namespace Payment.Api.Domains.PaymentLinks;

/// <summary>
/// 089/090: PaymentLink müşteri yüzeyinin sunum katmanı — hosted sayfaları string olarak inşa eder.
/// Routing'den AYRI (endpoint ince kalır): güven bağlamı + kart formu, 3D form, 3D hata sayfası.
/// Store-sağlanan metin HtmlEncode'lanır (XSS guard, İlke VI — sır render etmez). Mantık yok, sunum var.
/// </summary>
internal static class PaymentLinkPages
{
    // Güven bağlamı sayfası (FR-006/FR-008) — mağaza + sipariş + tutar + son geçerlilik, SONRA kart formu.
    // "Öde" kart alanlarını JSON gövdeyle POST /payment-links/{token}/pay → onayda ReturnUrl'e yönlendirir.
    // Store-sağlanan metin HtmlEncode'lanır (XSS guard, İlke VI sır render etmez).
    internal static string BuildTrustContextPage(string openToken, OpenPaymentLink.OpenPaymentLinkResponse ctx, PaymentLinkOptions options)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        var title = System.Net.WebUtility.HtmlEncode(options.TrustPageTitle);
        var intro = System.Net.WebUtility.HtmlEncode(options.TrustPageIntro);
        var store = System.Net.WebUtility.HtmlEncode(ctx.StoreName);
        var summary = System.Net.WebUtility.HtmlEncode(ctx.OrderSummary);
        var amount = System.Net.WebUtility.HtmlEncode(ctx.Amount.ToString("N2", tr) + " TL");
        var expires = System.Net.WebUtility.HtmlEncode(ctx.ExpiresAt.ToString("dd.MM.yyyy HH:mm", tr) + " (UTC)");

        return $$"""
        <!doctype html>
        <html lang="tr">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{title}}</title>
          <style>
            *{box-sizing:border-box}
            body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;
              background:#f4f5f7;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;
              color:#1f2937;padding:24px}
            .card{background:#fff;border-radius:16px;box-shadow:0 10px 30px rgba(0,0,0,.08);
              padding:32px;max-width:440px;width:100%}
            h1{margin:0 0 6px;font-size:20px;font-weight:600}
            .intro{margin:0 0 20px;font-size:14px;color:#6b7280}
            .row{display:flex;justify-content:space-between;gap:12px;padding:12px 0;border-top:1px solid #f0f1f3;font-size:15px}
            .row .k{color:#6b7280}
            .row .v{font-weight:600;text-align:right}
            .amount .v{font-size:20px;color:#111827}
            .form{margin-top:20px;display:grid;gap:10px}
            .form label{font-size:12px;color:#6b7280;display:block;margin-bottom:4px}
            .form input{width:100%;padding:11px 12px;border:1px solid #d1d5db;border-radius:8px;font-size:15px}
            .form .grid3{display:grid;grid-template-columns:1fr 1fr 1fr;gap:10px}
            button{appearance:none;border:0;cursor:pointer;font-size:16px;font-weight:600;width:100%;
              margin-top:20px;padding:14px;border-radius:10px;background:#111827;color:#fff;transition:background .15s}
            button:hover{background:#374151}
            button:disabled{background:#9ca3af;cursor:default}
            .msg{margin-top:16px;font-size:14px;color:#dc2626;text-align:center;display:none}
            .installments{margin-top:16px;display:grid;gap:8px}
            .installments .opt{display:flex;align-items:center;justify-content:space-between;gap:8px;padding:10px 12px;
              border:1px solid #d1d5db;border-radius:8px;font-size:14px;cursor:pointer}
            .installments .opt.sel{border-color:#111827;background:#f9fafb}
            .installments .bank{color:#6b7280;font-size:12px}
            .installments .tot{font-weight:600}
          </style>
        </head>
        <body>
          <div class="card">
            <h1>{{title}} — {{store}}</h1>
            <p class="intro">{{intro}}</p>
            <div class="row"><span class="k">Mağaza</span><span class="v">{{store}}</span></div>
            <div class="row"><span class="k">Sipariş</span><span class="v">{{summary}}</span></div>
            <div class="row amount"><span class="k">Tutar</span><span class="v">{{amount}}</span></div>
            <div class="row"><span class="k">Son geçerlilik</span><span class="v">{{expires}}</span></div>
            <div class="form">
              <div><label for="cardNumber">Kart Numarası</label><input id="cardNumber" inputmode="numeric" autocomplete="cc-number" placeholder="0000 0000 0000 0000" oninput="loadInstallments()"></div>
              <div><label for="holderName">Kart Üzerindeki İsim</label><input id="holderName" autocomplete="cc-name" placeholder="AD SOYAD"></div>
              <div class="grid3">
                <div><label for="expiryMonth">Ay</label><input id="expiryMonth" inputmode="numeric" autocomplete="cc-exp-month" placeholder="AA"></div>
                <div><label for="expiryYear">Yıl</label><input id="expiryYear" inputmode="numeric" autocomplete="cc-exp-year" placeholder="YY"></div>
                <div><label for="cvv">CVV</label><input id="cvv" inputmode="numeric" autocomplete="cc-csc" placeholder="123"></div>
              </div>
            </div>
            <div class="installments" id="installments"></div>
            <button id="payBtn" onclick="pay()">Öde</button>
            <div class="msg" id="msg"></div>
          </div>
          <script>
            // Kart alanlarını JSON gövdeyle /pay'e POST eder. Onayda ReturnUrl'e yönlendirir; redde normalize
            // mesaj gösterir (kart verisi / banka sırrı GÖSTERİLMEZ — İlke VI). Kart verisi yalnız bu istekte.
            var MSG={ PAYMENT_LINK_CHARGE_DECLINED:'Banka ödemeyi reddetti. Lütfen başka bir kartla deneyin.',
                      PAYMENT_LINK_PROVIDER_UNAVAILABLE:'Şu an işlem alınamadı. Lütfen tekrar deneyin.',
                      PAYMENT_LINK_EXPIRED:'Ödeme linkinin süresi doldu.',
                      PAYMENT_LINK_CANCELLED:'Bu ödeme linki iptal edildi.' };
            function val(id){ return document.getElementById(id).value.trim(); }
            // 093: seçilen taksit (1 = peşin). BIN girilince taksit seçenekleri (her biri en-avantajlı banka + tutar) yüklenir.
            var selectedInstallment=1;
            async function loadInstallments(){
              var box=document.getElementById('installments');
              var digits=val('cardNumber').replace(/\s+/g,'');
              if(digits.length<6){ box.innerHTML=''; selectedInstallment=1; return; }
              var bin=digits.slice(0,6);
              try{
                var r=await fetch(window.location.pathname + '/installments?bin=' + encodeURIComponent(bin));
                if(!r.ok){ box.innerHTML=''; return; }
                var d=await r.json(); renderInstallments((d&&d.options)||[]);
              }catch(e){ box.innerHTML=''; }
            }
            function renderInstallments(opts){
              var box=document.getElementById('installments');
              if(!opts.length){ box.innerHTML=''; return; }
              box.innerHTML='';
              opts.forEach(function(o,i){
                var row=document.createElement('div'); row.className='opt'+(i===0?' sel':'');
                var label=o.installment===1?'Tek Çekim':(o.installment+' Taksit');
                // Acquirer banka GÖSTERİLMEZ (gerçek-hayat UX; routing gizli iç karar). Yalnız taksit + tutar.
                row.innerHTML='<span><input type="radio" name="inst" value="'+o.installment+'"'+(i===0?' checked':'')+'> '+
                  label+'</span>'+
                  '<span class="tot">'+o.totalAmount.toFixed(2)+' TL'+(o.installment>1?' · aylık '+o.monthlyAmount.toFixed(2):'')+'</span>';
                row.onclick=function(){ row.querySelector('input').checked=true; select(o.installment); };
                row.querySelector('input').onchange=function(){ select(o.installment); };
                box.appendChild(row);
              });
              selectedInstallment=opts[0].installment;
            }
            function select(n){ selectedInstallment=n;
              document.querySelectorAll('#installments .opt').forEach(function(el){
                el.classList.toggle('sel', el.querySelector('input').checked); }); }
            async function pay(){
              var b=document.getElementById('payBtn'), m=document.getElementById('msg');
              b.disabled=true; b.textContent='İşleniyor...'; m.style.display='none';
              var payload={ cardNumber:val('cardNumber').replace(/\s+/g,''), holderName:val('holderName'),
                            expiryMonth:val('expiryMonth'), expiryYear:val('expiryYear'), cvv:val('cvv'),
                            installmentCount:selectedInstallment };
              try{
                var r=await fetch(window.location.pathname + '/pay',
                  {method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)});
                if(r.ok){ var d=await r.json(); window.location.href=d.redirectUrl; return; }
                var e=await r.json(); var code=(e.messages&&e.messages[0]&&e.messages[0].code)||'';
                m.textContent=MSG[code]||'Ödeme tamamlanamadı. Lütfen tekrar deneyin.';
              }catch(ex){ m.textContent='Bağlantı hatası. Lütfen tekrar deneyin.'; }
              m.style.display='block'; b.textContent='Öde'; b.disabled=false;
            }
          </script>
        </body>
        </html>
        """;
    }

    // 3D ödeme formu — kart alanları + İMZALI gizli alanlar; form DOĞRUDAN bankaya POST eder (action=banka
    // 3D ucu). PG sunucusu PAN'ı görmez (İlke VI; SAQ A-EP). Gizli alan değerleri banka üretimi (Hash dahil),
    // store metni yok → encode yalnız savunma. Sayfa yüklenince kart alanı boş; müşteri girer, "Öde" POST eder.
    internal static string Build3DFormPage(Initiate3DPayment.Response ctx)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        var store = System.Net.WebUtility.HtmlEncode(ctx.StoreName);
        var summary = System.Net.WebUtility.HtmlEncode(ctx.OrderSummary);
        var amount = System.Net.WebUtility.HtmlEncode(ctx.Amount.ToString("N2", tr) + " TL");
        var action = System.Net.WebUtility.HtmlEncode(ctx.ActionUrl);
        var hidden = string.Concat(ctx.Fields.Select(f =>
            $"<input type=\"hidden\" name=\"{System.Net.WebUtility.HtmlEncode(f.Key)}\" value=\"{System.Net.WebUtility.HtmlEncode(f.Value)}\">"));

        return $$"""
        <!doctype html>
        <html lang="tr">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
          <title>3D Güvenli Ödeme — {{store}}</title>
          <style>
            *{box-sizing:border-box}
            body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:#f4f5f7;
              font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;color:#1f2937;padding:24px}
            .card{background:#fff;border-radius:16px;box-shadow:0 10px 30px rgba(0,0,0,.08);padding:32px;max-width:440px;width:100%}
            h1{margin:0 0 6px;font-size:20px;font-weight:600}
            .row{display:flex;justify-content:space-between;padding:10px 0;border-top:1px solid #f0f1f3;font-size:15px}
            .row .k{color:#6b7280}.row .v{font-weight:600}
            label{font-size:12px;color:#6b7280;display:block;margin:12px 0 4px}
            input.c{width:100%;padding:11px 12px;border:1px solid #d1d5db;border-radius:8px;font-size:15px}
            .g3{display:grid;grid-template-columns:1fr 1fr;gap:10px}
            button{appearance:none;border:0;cursor:pointer;font-size:16px;font-weight:600;width:100%;margin-top:20px;
              padding:14px;border-radius:10px;background:#111827;color:#fff}
          </style>
        </head>
        <body>
          <div class="card">
            <h1>3D Güvenli Ödeme — {{store}}</h1>
            <div class="row"><span class="k">Sipariş</span><span class="v">{{summary}}</span></div>
            <div class="row"><span class="k">Tutar</span><span class="v">{{amount}}</span></div>
            <form method="post" action="{{action}}">
              {{hidden}}
              <label for="Pan">Kart Numarası</label>
              <input class="c" id="Pan" name="Pan" inputmode="numeric" autocomplete="cc-number" placeholder="0000 0000 0000 0000">
              <div class="g3">
                <div><label for="Expiry">Son Kullanma (AAYY)</label><input class="c" id="Expiry" name="Expiry" inputmode="numeric" placeholder="AAYY"></div>
                <div><label for="Cvv2">CVV</label><input class="c" id="Cvv2" name="Cvv2" inputmode="numeric" autocomplete="cc-csc" placeholder="123"></div>
              </div>
              <button type="submit">3D ile Öde</button>
            </form>
          </div>
        </body>
        </html>
        """;
    }

    // 3D callback başarısız (imza/3DStatus/red) → basit bilgilendirme sayfası (sır/kart/banka detayı YOK, İlke VI).
    internal static string Build3DResultPage()
        => """
        <!doctype html><html lang="tr"><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1"><title>Ödeme Tamamlanamadı</title>
        <style>body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:#f4f5f7;
        font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;color:#1f2937;padding:24px}
        .card{background:#fff;border-radius:16px;box-shadow:0 10px 30px rgba(0,0,0,.08);padding:32px;max-width:440px;text-align:center}
        h1{font-size:18px;margin:0 0 8px}p{color:#6b7280;font-size:14px;margin:0}</style></head>
        <body><div class="card"><h1>Ödeme tamamlanamadı</h1>
        <p>3D doğrulama başarısız oldu veya banka ödemeyi reddetti. Lütfen tekrar deneyin.</p></div></body></html>
        """;
}