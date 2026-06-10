#!/usr/bin/env bash
set -euo pipefail

# Checks the health of all microservices in the cluster by:
#   1. Verifying all pods are Running and Ready.
#   2. Calling /health/live and /health/ready on each service via kubectl exec.
#
# Usage:
#   ./deploy/scripts/health-check.sh [NAMESPACE]

NAMESPACE="${1:-ecommerce-prod}"

SERVICES=(
  order-service
  payment-service
  inventory-service
  shipping-service
  notification-service
  saga-orchestrator
)

INFRA=(
  postgres
  rabbitmq
  redis
  kong
)

log()  { echo "[health-check] $*"; }
pass() { echo "[health-check] PASS  $*"; }
fail() { echo "[health-check] FAIL  $*"; FAILURES=$((FAILURES + 1)); }

FAILURES=0

# ── Pod readiness ─────────────────────────────────────────────────────────────

log "Checking deployment rollout status..."
for svc in "${SERVICES[@]}" kong; do
  if kubectl rollout status deployment/"${svc}" -n "${NAMESPACE}" --timeout=60s &>/dev/null; then
    pass "Deployment ${svc} is rolled out"
  else
    fail "Deployment ${svc} is NOT fully rolled out"
  fi
done

log "Checking StatefulSet rollout status..."
for ss in postgres rabbitmq redis; do
  if kubectl rollout status statefulset/"${ss}" -n "${NAMESPACE}" --timeout=60s &>/dev/null; then
    pass "StatefulSet ${ss} is rolled out"
  else
    fail "StatefulSet ${ss} is NOT fully rolled out"
  fi
done

# ── Health endpoints ──────────────────────────────────────────────────────────

log "Probing /health/live and /health/ready on each service..."
for svc in "${SERVICES[@]}"; do
  POD=$(kubectl get pod -n "${NAMESPACE}" \
    -l "app=${svc}" \
    -o jsonpath='{.items[0].metadata.name}' 2>/dev/null || true)

  if [[ -z "${POD}" ]]; then
    fail "No pod found for ${svc}"
    continue
  fi

  if kubectl exec "${POD}" -n "${NAMESPACE}" -- \
      curl -sf http://localhost:8080/health/live -o /dev/null; then
    pass "${svc} /health/live"
  else
    fail "${svc} /health/live returned non-200"
  fi

  if kubectl exec "${POD}" -n "${NAMESPACE}" -- \
      curl -sf http://localhost:8080/health/ready -o /dev/null; then
    pass "${svc} /health/ready"
  else
    fail "${svc} /health/ready returned non-200"
  fi
done

# ── Summary ───────────────────────────────────────────────────────────────────

echo ""
log "All pods in ${NAMESPACE}:"
kubectl get pods -n "${NAMESPACE}"
echo ""

if [[ "${FAILURES}" -eq 0 ]]; then
  log "All checks passed."
  exit 0
else
  log "${FAILURES} check(s) failed."
  exit 1
fi
