Bir merchantin kendine ait yeni bir E-Ticaret sitesi vardır. Artık internet üzerinden satış yapmak istiyordur. Bu vesileyle bize gelir. Biz de merchant ile, kendisinin geleceği belli başlı kartlar üzerinden bir komisyon anlaşması imzalarız. Payment Gateway ayrıca belli başlı bankalar için de, belli durumlar karşılığında komisyon ödeyeceğini vaat eder ve banka komisyonları belirlenir. Payment Gateway merchant için kendi sisteminde bir kayıt açar. Merchant’a da belirli bir MerchantKey verir ki, kimin ödeme sistemine geldiği belli olsun. Payment Gateway’in UI tarafında, her bir Merchant kullanıcısı kendi işlem hareketliliğini görebilecek. Günün sonunda (EOD işlemlerinde) merchant’ın hesabına para aktarılacak. Günlük / Haftalık / Aylık işlem sayısı belirli saat aralıkları gibi dashboard veriler ekranda olabilecek. Para birimi TL olsun. Sistem yabancı para birimini desteklemesin.


Gerekli Olanlar

1- Her bir mercanta ait multitenant bir yapı olsa güzel olur. Merchant bazlı rol mekanizmasına gerek yok.
2- Payment Gateway’a ait ayrı bir rol mekanizması olması gerekecek.


Sorulacaklar

1- Settlement işleminden sonra Merchant’ın BankAccount’ına para nasıl aktarılacak?
2- E-Ticaret sitesinden müşteri işlem geçtiği zaman; müşterinin hesabından paranın çekilmesi, merchanta ve payment gateway’a para aktarılması nasıl gerçekleşecek?
3- Settlement sürecinde neler olmalı?