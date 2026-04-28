CREATE TABLE IF NOT EXISTS public.order_template (
    id SERIAL PRIMARY KEY,
    order_type_key VARCHAR(100) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    file_path VARCHAR(1000) NOT NULL,
    created_at TIMESTAMP DEFAULT NOW() NOT NULL
);

CREATE TABLE IF NOT EXISTS public.order_document (
    id BIGSERIAL PRIMARY KEY,
    order_type_key VARCHAR(100) NOT NULL,
    order_name VARCHAR(255) NOT NULL,
    program_id BIGINT REFERENCES public.learning_program(id) ON DELETE SET NULL,
    contract_id BIGINT REFERENCES public.contract(id) ON DELETE SET NULL,
    listener_id BIGINT REFERENCES public.person(id) ON DELETE SET NULL,
    teacher_id BIGINT REFERENCES public.teacher(id) ON DELETE SET NULL,
    document_number VARCHAR(100),
    file_name VARCHAR(500) NOT NULL,
    file_path VARCHAR(1000) NOT NULL,
    metadata_json TEXT,
    generated_at TIMESTAMP DEFAULT NOW() NOT NULL,
    created_at TIMESTAMP DEFAULT NOW() NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_order_document_program ON public.order_document(program_id);
CREATE INDEX IF NOT EXISTS idx_order_document_listener ON public.order_document(listener_id);
CREATE INDEX IF NOT EXISTS idx_order_document_teacher ON public.order_document(teacher_id);
CREATE INDEX IF NOT EXISTS idx_order_document_type ON public.order_document(order_type_key);

INSERT INTO public.order_template (order_type_key, name, file_path)
VALUES
    ('admission', 'О зачислении', 'C:\Dogovora\Приказы\Пример приказа о зачислении.docx'),
    ('admission_group', 'О зачислении группа', 'C:\Dogovora\Приказы\Пример приказа о зачислении группа.docx'),
    ('expulsion', 'Об отчислении', 'C:\Dogovora\Приказы\Пример приказа об отчислении.docx'),
    ('commission_composition', 'На состав комиссии', 'C:\Dogovora\Приказы\Пример приказа на состав комиссии.docx'),
    ('final_attestation_admission', 'О допуске итоговой аттестации', 'C:\Dogovora\Приказы\Пример приказа о допуске итоговой аттестации.docx'),
    ('commission_meeting_protocol', 'О заседании комиссии', 'C:\Dogovora\Приказы\Пример протокола заседании комиссии.docx'),
    ('statement', 'Ведомости', 'C:\Dogovora\Приказы\Пример ведомости.docx')
ON CONFLICT (order_type_key) DO UPDATE
SET
    name = EXCLUDED.name,
    file_path = EXCLUDED.file_path;
