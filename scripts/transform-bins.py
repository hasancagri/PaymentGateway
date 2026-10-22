#!/usr/bin/env python3
"""turkey-bin-list → Payment.Api BinCard seed (bin-seed.json) dönüştürücü.

Kaynak: https://github.com/berkaybucan/turkey-bin-list (ham: BIN/Network/Type/Category/Issuer).
Çıktı : BinCard şeması {Bin, IssuerBankCode, Scheme, CardType} — Domains/BinCards/bin-seed.json.

İssuer eşleme (on-us routing için gerçek banka → simülatör bankası):
  GARANTI → MAVIBANK,  IS BANKASI → SARIBANK,  YAPI KREDI → TURUNCUBANK.
Eşleşmeyen issuer'lar ATILIR (simülatörün 3 bankası dışı). Bilinen test BIN'leri (411111 vb.)
korunur — canlı/manuel demo kartları. BIN = Marten PK → tekilleştirilir, Bin'e göre sıralanır.

Kullanım:
  python3 scripts/transform-bins.py <ham-turkey-bin-list.json> [cikti=bin-seed.json yolu]
Çift-sarılı ([[...]]) ham girdi de kabul edilir (düzleştirilir).
"""
import json
import sys
from collections import Counter

DEFAULT_OUT = "src/services/Payment.Api/Domains/BinCards/bin-seed.json"

# Bilinen test BIN'leri — canlı/manuel demo için garanti tutulur (ham listede olmasa da).
TEST_BINS = [
    ("411111", "MAVIBANK", "Visa", "credit"),
    ("454360", "SARIBANK", "Visa", "credit"),
    ("400000", "MAVIBANK", "Visa", "credit"),
    ("979200", "SARIBANK", "Troy", "debit"),
    ("520000", "TURUNCUBANK", "Mastercard", "credit"),
]


def map_bank(issuer: str):
    u = issuer.upper()
    if "GARANTI" in u:
        return "MAVIBANK"
    if "IS BANKASI" in u:  # TARISBANK vb. kapsanmaz
        return "SARIBANK"
    if "YAPI" in u and "KREDI" in u and "SIGORTA" not in u:  # sigorta kaydını ele
        return "TURUNCUBANK"
    return None


def map_scheme(net: str) -> str:
    return {"VISA": "Visa", "MASTERCARD": "Mastercard", "TROY": "Troy", "MAESTRO": "Maestro"}.get(net, net.title())


def map_type(t: str):
    return {"DEBIT": "debit", "CREDIT": "credit"}.get(t)  # boş Type → None (atlanır)


def main() -> None:
    if len(sys.argv) < 2:
        sys.exit("kullanım: transform-bins.py <ham-turkey-bin-list.json> [cikti.json]")
    src, out = sys.argv[1], (sys.argv[2] if len(sys.argv) > 2 else DEFAULT_OUT)

    raw = json.load(open(src, encoding="utf-8"))
    rows = raw[0] if (isinstance(raw, list) and len(raw) == 1 and isinstance(raw[0], list)) else raw

    seen, result = set(), []
    for bin_, bank, scheme, ctype in TEST_BINS:  # test BIN'leri önce
        seen.add(bin_)
        result.append({"Bin": bin_, "IssuerBankCode": bank, "Scheme": scheme, "CardType": ctype})

    for r in rows:
        bank = map_bank(r.get("Issuer", ""))
        ctype = map_type(r.get("Type", ""))
        bin_ = str(r.get("BIN", "")).strip()
        if not bank or not ctype or not bin_ or bin_ in seen:
            continue
        seen.add(bin_)
        result.append({"Bin": bin_, "IssuerBankCode": bank, "Scheme": map_scheme(r.get("Network", "")), "CardType": ctype})

    result.sort(key=lambda x: x["Bin"])
    json.dump(result, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=2)

    print(f"yazıldı: {out}  ({len(result)} kayıt)")
    print("banka:", dict(Counter(x["IssuerBankCode"] for x in result)))
    print("tip:", dict(Counter(x["CardType"] for x in result)))


if __name__ == "__main__":
    main()