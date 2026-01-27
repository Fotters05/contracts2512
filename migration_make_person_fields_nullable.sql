-- Миграция для изменения обязательных полей таблицы person на опциональные
-- Выполните этот скрипт в вашей базе данных PostgreSQL

-- Изменяем поля на nullable
ALTER TABLE public.person
ALTER COLUMN date_of_birth DROP NOT NULL,
ALTER COLUMN gender_id DROP NOT NULL,
ALTER COLUMN citizenship DROP NOT NULL,
ALTER COLUMN snils DROP NOT NULL;

-- Комментарии к полям
COMMENT ON COLUMN public.person.date_of_birth IS 'Дата рождения (опционально)';
COMMENT ON COLUMN public.person.gender_id IS 'Пол (опционально)';
COMMENT ON COLUMN public.person.citizenship IS 'Гражданство (опционально)';
COMMENT ON COLUMN public.person.snils IS 'СНИЛС (опционально)';
