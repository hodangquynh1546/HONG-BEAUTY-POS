-- HONG BEAUTY POS: initial PostgreSQL schema. No production accounts or demo data are seeded.
-- Apply once in a transaction; record the checksum in the application's migration runner.
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email varchar(320) NOT NULL UNIQUE,
    password_hash text NOT NULL,
    full_name varchar(200) NOT NULL,
    role varchar(16) NOT NULL CHECK (role IN ('OWNER', 'EMPLOYEE')),
    active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX one_owner_only ON users (role) WHERE role = 'OWNER';

CREATE TABLE employee_permissions (
    user_id uuid NOT NULL REFERENCES users(id),
    permission varchar(80) NOT NULL,
    granted_by uuid NOT NULL REFERENCES users(id),
    granted_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, permission)
);

CREATE TABLE categories (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    parent_id uuid REFERENCES categories(id),
    name varchar(160) NOT NULL,
    active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE products (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category_id uuid REFERENCES categories(id),
    sku varchar(100) NOT NULL UNIQUE,
    barcode varchar(100) UNIQUE,
    name varchar(240) NOT NULL,
    description text,
    unit varchar(40) NOT NULL DEFAULT 'cái',
    cost_price numeric(18,2) NOT NULL DEFAULT 0 CHECK (cost_price >= 0),
    sale_price numeric(18,2) NOT NULL CHECK (sale_price >= 0),
    reorder_level numeric(18,3) NOT NULL DEFAULT 0 CHECK (reorder_level >= 0),
    active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0
);
CREATE INDEX products_name_idx ON products (lower(name));

CREATE TABLE stock_balances (
    product_id uuid PRIMARY KEY REFERENCES products(id),
    quantity numeric(18,3) NOT NULL DEFAULT 0 CHECK (quantity >= 0),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0
);

CREATE TABLE customers (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    full_name varchar(200) NOT NULL,
    phone varchar(40),
    email varchar(320),
    address text,
    notes text,
    active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX customers_phone_idx ON customers (phone);

CREATE TABLE suppliers (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(80) NOT NULL UNIQUE,
    name varchar(200) NOT NULL,
    phone varchar(40),
    email varchar(320),
    address text,
    active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE purchase_receipts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    receipt_no varchar(80) NOT NULL UNIQUE,
    supplier_id uuid REFERENCES suppliers(id),
    status varchar(16) NOT NULL DEFAULT 'DRAFT' CHECK (status IN ('DRAFT', 'COMPLETED', 'CANCELLED')),
    total numeric(18,2) NOT NULL DEFAULT 0 CHECK (total >= 0),
    created_by uuid NOT NULL REFERENCES users(id),
    completed_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0
);

CREATE TABLE purchase_receipt_items (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    receipt_id uuid NOT NULL REFERENCES purchase_receipts(id),
    product_id uuid NOT NULL REFERENCES products(id),
    quantity numeric(18,3) NOT NULL CHECK (quantity > 0),
    unit_cost numeric(18,2) NOT NULL CHECK (unit_cost >= 0),
    discount numeric(18,2) NOT NULL DEFAULT 0 CHECK (discount >= 0),
    UNIQUE (receipt_id, product_id)
);

CREATE TABLE sales_orders (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    order_no varchar(80) NOT NULL UNIQUE,
    idempotency_key uuid NOT NULL UNIQUE,
    customer_id uuid REFERENCES customers(id),
    sold_by uuid NOT NULL REFERENCES users(id),
    status varchar(20) NOT NULL CHECK (status IN ('DRAFT', 'UNPAID', 'PARTIALLY_PAID', 'PAID', 'CANCELLED', 'PARTIALLY_RETURNED', 'RETURNED')),
    subtotal numeric(18,2) NOT NULL CHECK (subtotal >= 0),
    discount numeric(18,2) NOT NULL DEFAULT 0 CHECK (discount >= 0),
    total numeric(18,2) NOT NULL CHECK (total >= 0),
    paid_total numeric(18,2) NOT NULL DEFAULT 0 CHECK (paid_total >= 0),
    notes text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0,
    CHECK (paid_total <= total)
);
CREATE INDEX sales_orders_created_idx ON sales_orders (created_at DESC);

CREATE TABLE sales_order_items (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id uuid NOT NULL REFERENCES sales_orders(id),
    product_id uuid NOT NULL REFERENCES products(id),
    sku_snapshot varchar(100) NOT NULL,
    name_snapshot varchar(240) NOT NULL,
    quantity numeric(18,3) NOT NULL CHECK (quantity > 0),
    unit_price numeric(18,2) NOT NULL CHECK (unit_price >= 0),
    unit_cost numeric(18,2) NOT NULL CHECK (unit_cost >= 0),
    discount numeric(18,2) NOT NULL DEFAULT 0 CHECK (discount >= 0),
    UNIQUE (order_id, product_id)
);

CREATE TABLE payments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id uuid NOT NULL REFERENCES sales_orders(id),
    method varchar(20) NOT NULL CHECK (method IN ('CASH', 'BANK_TRANSFER', 'QR_BANKING')),
    amount numeric(18,2) NOT NULL CHECK (amount > 0),
    reference varchar(160),
    received_by uuid NOT NULL REFERENCES users(id),
    received_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX payments_order_idx ON payments (order_id);

CREATE TABLE sale_returns (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    return_no varchar(80) NOT NULL UNIQUE,
    order_id uuid NOT NULL REFERENCES sales_orders(id),
    reason text NOT NULL,
    refund_amount numeric(18,2) NOT NULL CHECK (refund_amount >= 0),
    created_by uuid NOT NULL REFERENCES users(id),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE sale_return_items (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    return_id uuid NOT NULL REFERENCES sale_returns(id),
    order_item_id uuid NOT NULL REFERENCES sales_order_items(id),
    quantity numeric(18,3) NOT NULL CHECK (quantity > 0),
    restock boolean NOT NULL,
    UNIQUE (return_id, order_item_id)
);

CREATE TABLE stock_movements (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id uuid NOT NULL REFERENCES products(id),
    movement_type varchar(32) NOT NULL CHECK (movement_type IN ('PURCHASE', 'SALE', 'SALE_RETURN', 'DAMAGE', 'ADJUSTMENT')),
    quantity_delta numeric(18,3) NOT NULL CHECK (quantity_delta <> 0),
    quantity_before numeric(18,3) NOT NULL CHECK (quantity_before >= 0),
    quantity_after numeric(18,3) NOT NULL CHECK (quantity_after >= 0),
    source_id uuid NOT NULL,
    performed_by uuid NOT NULL REFERENCES users(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    CHECK (quantity_after = quantity_before + quantity_delta)
);
CREATE INDEX stock_movements_product_time_idx ON stock_movements (product_id, created_at DESC);

CREATE TABLE audit_logs (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_id uuid REFERENCES users(id),
    action varchar(100) NOT NULL,
    entity_type varchar(80) NOT NULL,
    entity_id uuid,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX audit_logs_time_idx ON audit_logs (created_at DESC);
