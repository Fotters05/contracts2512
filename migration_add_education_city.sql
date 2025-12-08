-- Миграция для добавления поля city в таблицу education
-- Выполните этот скрипт в вашей базе данных PostgreSQL

ALTER TABLE public.education
ADD COLUMN IF NOT EXISTS city VARCHAR(200);

-- Комментарий к полю
COMMENT ON COLUMN public.education.city IS 'Город получения образования';

