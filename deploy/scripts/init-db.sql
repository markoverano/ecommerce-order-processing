-- Creates all service databases and dedicated least-privilege users on first PostgreSQL startup.
-- Idempotent: uses IF NOT EXISTS so it can run safely on re-init.
-- Passwords are injected via the ORDER_DB_PASSWORD etc. environment variables in docker-compose.

CREATE DATABASE orders_db;
CREATE DATABASE payments_db;
CREATE DATABASE inventory_db;
CREATE DATABASE shipping_db;
CREATE DATABASE notifications_db;
CREATE DATABASE saga_db;
CREATE DATABASE analytics_db;

-- Per-service users: each user can only connect to its own database.
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'order_svc') THEN
        CREATE USER order_svc WITH PASSWORD :'ORDER_DB_PASSWORD';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'payment_svc') THEN
        CREATE USER payment_svc WITH PASSWORD :'PAYMENT_DB_PASSWORD';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'inventory_svc') THEN
        CREATE USER inventory_svc WITH PASSWORD :'INVENTORY_DB_PASSWORD';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'shipping_svc') THEN
        CREATE USER shipping_svc WITH PASSWORD :'SHIPPING_DB_PASSWORD';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'notification_svc') THEN
        CREATE USER notification_svc WITH PASSWORD :'NOTIFICATION_DB_PASSWORD';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'saga_svc') THEN
        CREATE USER saga_svc WITH PASSWORD :'SAGA_DB_PASSWORD';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'analytics_svc') THEN
        CREATE USER analytics_svc WITH PASSWORD :'ANALYTICS_DB_PASSWORD';
    END IF;
END
$$;

GRANT ALL PRIVILEGES ON DATABASE orders_db TO order_svc;
GRANT ALL PRIVILEGES ON DATABASE payments_db TO payment_svc;
GRANT ALL PRIVILEGES ON DATABASE inventory_db TO inventory_svc;
GRANT ALL PRIVILEGES ON DATABASE shipping_db TO shipping_svc;
GRANT ALL PRIVILEGES ON DATABASE notifications_db TO notification_svc;
GRANT ALL PRIVILEGES ON DATABASE saga_db TO saga_svc;
GRANT ALL PRIVILEGES ON DATABASE analytics_db TO analytics_svc;
