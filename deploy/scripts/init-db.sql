-- Creates all service databases on first PostgreSQL startup.
-- Idempotent: uses IF NOT EXISTS so it can run safely on re-init.

CREATE DATABASE orders_db;
CREATE DATABASE payments_db;
CREATE DATABASE inventory_db;
CREATE DATABASE shipping_db;
CREATE DATABASE notifications_db;
CREATE DATABASE saga_db;
