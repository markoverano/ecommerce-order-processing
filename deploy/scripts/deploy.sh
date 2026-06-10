#!/usr/bin/env bash
set -euo pipefail

# Usage:
#   ./deploy/scripts/deploy.sh [REGISTRY] [TAG]
#
# REGISTRY defaults to ghcr.io/your-org/ecommerce — set it to your actual registry.
# TAG defaults to the short git commit SHA.
#
# Prerequisites:
#   - docker (logged in to REGISTRY)
#   - kubectl (configured for the target cluster)
#   - envsubst (part of gettext)

REGISTRY="${1:-${REGISTRY:-ghcr.io/your-org/ecommerce}}"
IMAGE_TAG="${2:-${IMAGE_TAG:-$(git rev-parse --short HEAD)}}"
NAMESPACE="ecommerce-prod"

SERVICES=(
  order-service
  payment-service
  inventory-service
  shipping-service
  notification-service
  saga-orchestrator
)

# Maps kebab-case service name to the Dockerfile path under src/.
declare -A DOCKERFILES=(
  [order-service]="src/OrderService/Api/Dockerfile"
  [payment-service]="src/PaymentService/Api/Dockerfile"
  [inventory-service]="src/InventoryService/Api/Dockerfile"
  [shipping-service]="src/ShippingService/Api/Dockerfile"
  [notification-service]="src/NotificationService/Api/Dockerfile"
  [saga-orchestrator]="src/SagaOrchestrator/Api/Dockerfile"
)

log() { echo "[deploy] $*"; }

# ── Build and push images ────────────────────────────────────────────────────

log "Building and pushing images (registry=${REGISTRY}, tag=${IMAGE_TAG})"

for svc in "${SERVICES[@]}"; do
  log "  Building ${svc} (${DOCKERFILES[$svc]})..."
  docker build \
    --file "${DOCKERFILES[$svc]}" \
    --tag "${REGISTRY}/${svc}:${IMAGE_TAG}" \
    --tag "${REGISTRY}/${svc}:latest" \
    .
  docker push "${REGISTRY}/${svc}:${IMAGE_TAG}"
  docker push "${REGISTRY}/${svc}:latest"
done

# ── Apply namespace first ────────────────────────────────────────────────────

log "Applying namespace"
kubectl apply -f deploy/k8s/namespace.yaml

# ── Apply infra manifests ────────────────────────────────────────────────────

log "Applying infrastructure (postgres, rabbitmq, redis)"
kubectl apply -f deploy/k8s/infra/postgres.yaml
kubectl apply -f deploy/k8s/infra/rabbitmq.yaml
kubectl apply -f deploy/k8s/infra/redis.yaml

log "Waiting for postgres to be ready..."
kubectl rollout status statefulset/postgres -n "${NAMESPACE}" --timeout=120s

log "Waiting for rabbitmq to be ready..."
kubectl rollout status statefulset/rabbitmq -n "${NAMESPACE}" --timeout=120s

# ── Create Kong ConfigMap from the declarative config file ──────────────────

log "Creating kong-config ConfigMap from deploy/kong/kong.yml"
kubectl create configmap kong-config \
  --from-file=kong.yml=deploy/kong/kong.yml \
  --namespace="${NAMESPACE}" \
  --dry-run=client -o yaml | kubectl apply -f -

# ── Apply service manifests with image substitution ─────────────────────────

log "Applying service manifests (REGISTRY=${REGISTRY}, IMAGE_TAG=${IMAGE_TAG})"

for svc in "${SERVICES[@]}"; do
  log "  Applying ${svc}..."
  sed -e "s|REGISTRY|${REGISTRY}|g" -e "s|IMAGE_TAG|${IMAGE_TAG}|g" \
    "deploy/k8s/${svc}/deployment.yaml" | kubectl apply -f -
done

# ── Apply Kong ───────────────────────────────────────────────────────────────

log "Applying Kong API gateway"
kubectl apply -f deploy/k8s/infra/kong.yaml

# ── Roll out and wait ────────────────────────────────────────────────────────

log "Waiting for service rollouts to complete..."
for svc in "${SERVICES[@]}"; do
  kubectl rollout status deployment/"${svc}" -n "${NAMESPACE}" --timeout=300s
done

kubectl rollout status deployment/kong -n "${NAMESPACE}" --timeout=120s

log "Deploy complete. All pods:"
kubectl get pods -n "${NAMESPACE}"
