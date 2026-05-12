#!/bin/bash
# Waits for RabbitMQ management API to be ready, then creates the exchange and queues.
set -euo pipefail

RABBIT_HOST="${RABBITMQ_HOST:-localhost}"
RABBIT_PORT="${RABBITMQ_MGMT_PORT:-15672}"
RABBIT_USER="${RABBITMQ_USER:-guest}"
RABBIT_PASS="${RABBITMQ_PASS:-guest}"
BASE="http://${RABBIT_HOST}:${RABBIT_PORT}/api"

wait_for_rabbitmq() {
  echo "Waiting for RabbitMQ management API at ${BASE}..."
  until curl -s -u "${RABBIT_USER}:${RABBIT_PASS}" "${BASE}/overview" > /dev/null 2>&1; do
    sleep 2
  done
  echo "RabbitMQ is ready."
}

create_exchange() {
  local name="$1"
  echo "Creating exchange: ${name}"
  curl -s -u "${RABBIT_USER}:${RABBIT_PASS}" \
    -X PUT "${BASE}/exchanges/%2F/${name}" \
    -H "Content-Type: application/json" \
    -d '{"type":"topic","durable":true,"auto_delete":false}'
}

create_queue() {
  local name="$1"
  echo "Creating queue: ${name}"
  curl -s -u "${RABBIT_USER}:${RABBIT_PASS}" \
    -X PUT "${BASE}/queues/%2F/${name}" \
    -H "Content-Type: application/json" \
    -d '{"durable":true,"auto_delete":false}'
}

bind_queue() {
  local queue="$1"
  local exchange="$2"
  local routing_key="$3"
  echo "Binding ${queue} to ${exchange} with key ${routing_key}"
  curl -s -u "${RABBIT_USER}:${RABBIT_PASS}" \
    -X POST "${BASE}/bindings/%2F/e/${exchange}/q/${queue}" \
    -H "Content-Type: application/json" \
    -d "{\"routing_key\":\"${routing_key}\"}"
}

wait_for_rabbitmq
create_exchange "order.events"

# Service queues
create_queue "order-service.events"
create_queue "payment-service.commands"
create_queue "payment-service.events"
create_queue "inventory-service.commands"
create_queue "inventory-service.events"
create_queue "shipping-service.commands"
create_queue "shipping-service.events"
create_queue "notification-service.commands"
create_queue "notification-service.events"
create_queue "saga-orchestrator.events"

# Bindings: order events → saga orchestrator
bind_queue "saga-orchestrator.events" "order.events" "order.created"
bind_queue "saga-orchestrator.events" "order.events" "payment.processed"
bind_queue "saga-orchestrator.events" "order.events" "payment.failed"
bind_queue "saga-orchestrator.events" "order.events" "payment.refunded"
bind_queue "saga-orchestrator.events" "order.events" "stock.reserved"
bind_queue "saga-orchestrator.events" "order.events" "stock.out"
bind_queue "saga-orchestrator.events" "order.events" "shipment.created"
bind_queue "saga-orchestrator.events" "order.events" "shipment.failed"
bind_queue "saga-orchestrator.events" "order.events" "notification.sent"

# Bindings: saga commands → downstream services
bind_queue "payment-service.commands" "order.events" "command.process-payment"
bind_queue "payment-service.commands" "order.events" "command.refund-payment"
bind_queue "inventory-service.commands" "order.events" "command.reserve-stock"
bind_queue "inventory-service.commands" "order.events" "command.release-stock"
bind_queue "shipping-service.commands" "order.events" "command.create-shipment"
bind_queue "shipping-service.commands" "order.events" "command.cancel-shipment"
bind_queue "notification-service.commands" "order.events" "command.notify-customer"

echo "RabbitMQ setup complete."
