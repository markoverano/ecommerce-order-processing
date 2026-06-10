#!/usr/bin/env bash
set -euo pipefail

# Rolls back all microservice Deployments to their previous revision.
# Infra StatefulSets (postgres, rabbitmq, redis) are intentionally excluded
# because rolling back stateful workloads requires data-aware procedures.
#
# Usage:
#   ./deploy/scripts/rollback.sh [NAMESPACE]

NAMESPACE="${1:-ecommerce-prod}"

SERVICES=(
  order-service
  payment-service
  inventory-service
  shipping-service
  notification-service
  saga-orchestrator
  kong
)

log() { echo "[rollback] $*"; }

log "Rolling back all deployments in namespace ${NAMESPACE}"

for svc in "${SERVICES[@]}"; do
  if kubectl get deployment "${svc}" -n "${NAMESPACE}" &>/dev/null; then
    log "  Rolling back ${svc}..."
    kubectl rollout undo deployment/"${svc}" -n "${NAMESPACE}"
  else
    log "  Skipping ${svc} — deployment not found"
  fi
done

log "Waiting for rollbacks to complete..."
for svc in "${SERVICES[@]}"; do
  if kubectl get deployment "${svc}" -n "${NAMESPACE}" &>/dev/null; then
    kubectl rollout status deployment/"${svc}" -n "${NAMESPACE}" --timeout=300s
  fi
done

log "Rollback complete. Current pod state:"
kubectl get pods -n "${NAMESPACE}"
