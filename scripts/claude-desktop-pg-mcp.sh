#!/usr/bin/env bash
# Claude Desktop → PG MCP köprüsü (test amaçlı, 044 canlı doğrulama).
# PG servislerinde PRM (RFC 9728) henüz yok → mcp-remote'un interaktif OAuth keşfi çalışmaz.
# Bu script açılışta Identity.Server'dan client_credentials (admin-ui) token'ı alır ve
# mcp-remote'a statik Authorization header'ı olarak geçirir. Token ömrü 15 dk — süresi dolunca
# tool çağrıları 401 döner; Claude Desktop'ı yeniden başlatmak yeni token üretir.
# Kalıcı çözüm: PG MCP fasadı + PRM (EC 073 deseni, ileriki spec).
set -euo pipefail

SURFACE="${1:?kullanım: claude-desktop-pg-mcp.sh merchant|commission}"

case "$SURFACE" in
  merchant)
    URL="http://localhost:5202/mcp"
    SCOPE="merchant.read merchant.write merchant.admin"
    ;;
  commission)
    URL="http://localhost:5203/mcp"
    SCOPE="commission.read commission.write"
    ;;
  *)
    echo "bilinmeyen yüzey: $SURFACE (merchant|commission)" >&2
    exit 1
    ;;
esac

TOKEN=$(curl -sk -X POST https://localhost:5101/connect/token \
  -d grant_type=client_credentials \
  -d client_id=admin-ui \
  -d client_secret=admin-ui-dev-secret \
  --data-urlencode "scope=${SCOPE}" \
  | /usr/bin/python3 -c "import sys,json;print(json.load(sys.stdin)['access_token'])")

exec npx -y mcp-remote@0.8.6 "$URL" --allow-http --header "Authorization: Bearer ${TOKEN}"