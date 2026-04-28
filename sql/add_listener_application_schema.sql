CREATE TABLE IF NOT EXISTS public.listener_application (
    id BIGSERIAL PRIMARY KEY,
    application_type_key VARCHAR(100) NOT NULL,
    contract_id BIGINT NOT NULL REFERENCES public.contract(id) ON DELETE CASCADE,
    listener_id BIGINT NOT NULL REFERENCES public.person(id) ON DELETE RESTRICT,
    program_id BIGINT NOT NULL REFERENCES public.learning_program(id) ON DELETE RESTRICT,
    order_document_id BIGINT REFERENCES public.order_document(id) ON DELETE SET NULL,
    application_number VARCHAR(100),
    application_date DATE NOT NULL,
    notes TEXT,
    created_at TIMESTAMP DEFAULT NOW() NOT NULL,
    updated_at TIMESTAMP DEFAULT NOW() NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_listener_application_listener
    ON public.listener_application(listener_id);

CREATE INDEX IF NOT EXISTS idx_listener_application_program
    ON public.listener_application(program_id);

CREATE INDEX IF NOT EXISTS idx_listener_application_order_document
    ON public.listener_application(order_document_id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_listener_application_contract_type
    ON public.listener_application(contract_id, application_type_key);
