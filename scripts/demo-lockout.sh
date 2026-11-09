#!/usr/bin/env bash
# Demo khóa tài khoản (lớp 3 trong demo/16): 10 lần sai mật khẩu, rồi đúng mật khẩu vẫn bị khóa 15 phút.
# Gặp 429 (rate limit 5 lần/phút/IP) thì tự chờ theo Retry-After rồi gửi lại, nên mất khoảng 2 phút.
#
#   ./scripts/demo-lockout.sh <email> [base-url]
#   ./scripts/demo-lockout.sh john@example.com https://ebay.petlify.xyz
#
# Dùng tài khoản phụ, KHÔNG dùng superadmin: tài khoản bị khóa thật 15 phút.
set -u
email=${1:?Cách dùng: $0 <email> [base-url]}
base=${2:-https://ebay.petlify.xyz}
read -rsp "Mật khẩu ĐÚNG của $email (dùng ở bước cuối): " password; echo

# In body, gặp 429 thì chờ rồi gửi lại.
login() {
  local body headers code wait
  headers=$(mktemp)
  while :; do
    body=$(curl -s -D "$headers" "$base/api/Auth/login" -H "Content-Type: application/json" \
      --data "$(printf '{"email":"%s","password":"%s"}' "$email" "$1")")
    code=$(awk 'NR==1{print $2}' "$headers")
    [ "$code" != "429" ] && break
    wait=$(grep -i '^retry-after:' "$headers" | tr -dc '0-9'); wait=${wait:-10}
    src=$(grep -i '^x-ratelimit-source:' "$headers" | cut -d' ' -f2 | tr -d '\r')
    echo "    429 do $src chặn (rate limit theo IP), chờ ${wait}s rồi gửi lại..." >&2
    sleep "$wait"
  done
  rm -f "$headers"
  echo "$body"
}

msg() { sed -n 's/.*"errorMessage":"\([^"]*\)".*/\1/p'; }

for i in $(seq 1 10); do
  echo "Lần $i/10 sai mật khẩu: $(login "sai-mat-khau-$i" | msg)"
done

echo
echo ">>> Đăng nhập với mật khẩu ĐÚNG:"
result=$(login "$password")
echo "    $(echo "$result" | msg)"
case "$result" in
  *"bị khóa"*) echo "OK: đúng mật khẩu vẫn bị chặn, khóa tính theo email nên đổi IP cũng không lách được." ;;
  *) echo "Không thấy thông báo khóa. Body: $result" ;;
esac

key="ebay-count:login-fail:$(echo "$email" | tr '[:upper:]' '[:lower:]')"
echo
echo "Gỡ khóa ngay (trên VPS): docker exec ebay_redis redis-cli DEL \"$key\""
