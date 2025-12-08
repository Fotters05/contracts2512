-- Миграция для добавления полей опций в таблицу contract
-- Выполните этот скрипт в вашей базе данных PostgreSQL

ALTER TABLE public.contract
ADD COLUMN IF NOT EXISTS itog_document_option_key VARCHAR(50),
ADD COLUMN IF NOT EXISTS time_option_key VARCHAR(50),
ADD COLUMN IF NOT EXISTS study_option_key VARCHAR(50),
ADD COLUMN IF NOT EXISTS signer_id INTEGER,
ADD COLUMN IF NOT EXISTS payment_option_key VARCHAR(50);

-- Комментарии к полям
COMMENT ON COLUMN public.contract.itog_document_option_key IS 'Ключ выбранной опции итогового документа (для ПК/ПП)';
COMMENT ON COLUMN public.contract.time_option_key IS 'Ключ выбранной опции учебной нагрузки (для ПК/ПП)';
COMMENT ON COLUMN public.contract.study_option_key IS 'Ключ выбранной опции учебной нагрузки (для других типов договоров)';
COMMENT ON COLUMN public.contract.signer_id IS 'ID выбранного подписанта';
COMMENT ON COLUMN public.contract.payment_option_key IS 'Ключ выбранной опции оплаты';

