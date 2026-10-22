#!/usr/bin/env bash
# Demo: deploy the API from the image running now (v1) to a second tag (v2), then roll back.
#
# Both tags must already exist in GHCR. This script does not build or push.
#   ./scripts/demo-rolling-update.sh sha-abc1234
#
# Optional: BASE_URL=http://localhost ./scripts/demo-rolling-update.sh sha-abc1234
set -euo pipefail

NAMESPACE=ebay-clone
DEPLOYMENT=ebay-api
CONTAINER=ebay-api
IMAGE_REPO=ghcr.io/monomee/ebay_clone_adminrole
BASE_URL="${BASE_URL:-http://localhost}"

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <v2-tag>" >&2
  echo "Example: $0 sha-abc1234" >&2
  exit 1
fi

V2_IMAGE="${IMAGE_REPO}:$1"
V1_IMAGE="$(kubectl -n "$NAMESPACE" get deployment "$DEPLOYMENT" -o jsonpath='{.spec.template.spec.containers[0].image}')"

if [[ -z "$V1_IMAGE" ]]; then
  echo "Could not read the current image. Is the deployment running?" >&2
  exit 1
fi

if [[ "$V1_IMAGE" == "$V2_IMAGE" ]]; then
  echo "v2 is already the running image ($V1_IMAGE)." >&2
  exit 1
fi

echo "v1 (current): $V1_IMAGE"
echo "v2 (target):  $V2_IMAGE"
echo

echo "==> Deploy v2"
kubectl -n "$NAMESPACE" set image "deployment/${DEPLOYMENT}" "${CONTAINER}=${V2_IMAGE}"
kubectl -n "$NAMESPACE" rollout status "deployment/${DEPLOYMENT}" --timeout=300s

echo
echo "==> Probe ${BASE_URL}/health/ready (expect HTTP 200 and X-Backend-Server)"
for _ in 1 2 3 4 5; do
  curl -sS -D - -o /dev/null "${BASE_URL}/health/ready" \
    | tr -d '\r' \
    | awk 'BEGIN{IGNORECASE=1} /^HTTP\//{code=$0} tolower($1)=="x-backend-server:"{hdr=$0} END{print code; if (hdr) print hdr; else print "x-backend-server: (header missing)"}'
  echo
done

echo "==> Roll back to v1"
kubectl -n "$NAMESPACE" rollout undo "deployment/${DEPLOYMENT}"
kubectl -n "$NAMESPACE" rollout status "deployment/${DEPLOYMENT}" --timeout=300s

echo
echo "==> Image after undo"
AFTER="$(kubectl -n "$NAMESPACE" get deployment "$DEPLOYMENT" -o jsonpath='{.spec.template.spec.containers[0].image}')"
echo "$AFTER"
if [[ "$AFTER" != "$V1_IMAGE" ]]; then
  echo "Undo finished, but the image is not the v1 captured at the start." >&2
  exit 1
fi

echo
kubectl -n "$NAMESPACE" rollout history "deployment/${DEPLOYMENT}"
