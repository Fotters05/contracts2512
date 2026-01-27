-- Миграция для изменения обязательных полей таблицы passport на опциональные
-- Выполните этот скрипт в вашей базе данных PostgreSQL

-- Изменяем поля на nullable
ALTER TABLE public.passport
ALTER COLUMN series DROP NOT NULL,
ALTER COLUMN number DROP NOT NULL,
ALTER COLUMN issuance_date DROP NOT NULL,
ALTER COLUMN issued_by DROP NOT NULL,
ALTER COLUMN division_code DROP NOT NULL,
ALTER COLUMN registration_date DROP NOT NULL;

-- Комментарии к полям
COMMENT ON COLUMN public.passport.series IS 'Серия паспорта (опционально)';
COMMENT ON COLUMN public.passport.number IS 'Номер паспорта (опционально)';
COMMENT ON COLUMN public.passport.issuance_date IS 'Дата выдачи паспорта (опционально)';
COMMENT ON COLUMN public.passport.issued_by IS 'Кем выдан паспорт (опционально)';
COMMENT ON COLUMN public.passport.division_code IS 'Код подразделения (опционально)';
COMMENT ON COLUMN public.passport.registration_date IS 'Дата регистрации (опционально)';
