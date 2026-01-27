-- Миграция для изменения обязательных полей таблицы education на опциональные
-- Выполните этот скрипт в вашей базе данных PostgreSQL

-- Изменяем поля на nullable
ALTER TABLE public.education
ALTER COLUMN enrollment_date DROP NOT NULL;

-- Комментарии к полям
COMMENT ON COLUMN public.education.enrollment_date IS 'Дата зачисления (опционально)';
