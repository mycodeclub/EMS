#!/usr/bin/env bash
# Gets (or renews) the Let's Encrypt certificate for the production site and exports it as a .pfx to import in the
# site4now panel (SSL → Import). Let's Encrypt proves we own the domain by fetching a file from
# http://<domain>/.well-known/acme-challenge/; this script uploads that file over FTPS, the same way deploy.yml uploads
# the site. Certificates last 90 days: run it again after about 60.
#
#   brew install certbot
#   scripts/ssl-certificate.sh              # asks for the FTP login (the FTP_USERNAME / FTP_PASSWORD GitHub secrets)
#
# Everything (account, keys, .pfx) is kept in ~/.ems-ssl, outside the repository.
set -euo pipefail

DOMAIN="${SSL_DOMAIN:-ems.bitprosofttech.com}"
FTP_SERVER="${FTP_SERVER:-win8117.site4now.net}"
FTP_REMOTE_DIR="${FTP_REMOTE_DIR:-ems/}"
STATE_DIR="${EMS_SSL_DIR:-$HOME/.ems-ssl}"
CHALLENGE_DIR="${FTP_REMOTE_DIR#/}.well-known/acme-challenge"

ftp() { curl -sS --ssl-reqd --netrc-file "$EMS_SSL_NETRC" "$@"; }

case "${1:-}" in
  auth)
    # certbot hook: publish the challenge file and wait until the site serves it.
    printf '%s' "$CERTBOT_VALIDATION" | ftp --ftp-create-dirs -T - "ftp://$FTP_SERVER/$CHALLENGE_DIR/$CERTBOT_TOKEN"
    for _ in $(seq 1 15); do
      [ "$(curl -s -m 10 "http://$DOMAIN/.well-known/acme-challenge/$CERTBOT_TOKEN")" = "$CERTBOT_VALIDATION" ] && exit 0
      sleep 2
    done
    echo "The site does not serve the uploaded challenge file at http://$DOMAIN/.well-known/acme-challenge/" >&2
    exit 1 ;;
  cleanup)
    ftp "ftp://$FTP_SERVER/" -Q "DELE $CHALLENGE_DIR/$CERTBOT_TOKEN" -o /dev/null || true
    exit 0 ;;
esac

command -v certbot >/dev/null || { echo "certbot is missing: brew install certbot" >&2; exit 1; }

read -rp "FTP username: " ftp_user
read -rsp "FTP password: " ftp_password; echo
mkdir -p "$STATE_DIR"; chmod 700 "$STATE_DIR"
EMS_SSL_NETRC="$(mktemp)"; export EMS_SSL_NETRC
trap 'rm -f "$EMS_SSL_NETRC"' EXIT
printf 'machine %s login %s password %s\n' "$FTP_SERVER" "$ftp_user" "$ftp_password" > "$EMS_SSL_NETRC"
ftp "ftp://$FTP_SERVER/" -o /dev/null || { echo "FTP login failed." >&2; exit 1; }

self="$(cd "$(dirname "$0")" && pwd)/$(basename "$0")"
certbot certonly --manual --preferred-challenges http -d "$DOMAIN" \
  --manual-auth-hook "'$self' auth" --manual-cleanup-hook "'$self' cleanup" \
  --non-interactive --agree-tos --register-unsafely-without-email --keep-until-expiring \
  --config-dir "$STATE_DIR/config" --work-dir "$STATE_DIR/work" --logs-dir "$STATE_DIR/logs" ${@+"$@"}

# 3DES/SHA1 encryption so every Windows Server version can import the .pfx (OpenSSL's AES default fails on older ones).
live="$STATE_DIR/config/live/$DOMAIN"
pfx="$STATE_DIR/$DOMAIN-$(date +%Y-%m-%d).pfx"
pfx_password="$(openssl rand -base64 18 | tr -d '/+=')"
openssl pkcs12 -export -in "$live/fullchain.pem" -inkey "$live/privkey.pem" -out "$pfx" -name "$DOMAIN" \
  -certpbe PBE-SHA1-3DES -keypbe PBE-SHA1-3DES -macalg sha1 -passout "pass:$pfx_password"
printf '%s\n' "$pfx_password" > "$pfx.password.txt"; chmod 600 "$pfx" "$pfx.password.txt"

echo
openssl x509 -in "$live/cert.pem" -noout -subject -issuer -enddate
echo "PFX:      $pfx"
echo "Password: $pfx_password   (also in $pfx.password.txt)"
echo "Import it in the site4now panel: SSL → Import SSL, for $DOMAIN."
