-- Миграция для обновления таблиц contacts и passport
-- Выполните этот скрипт в вашей базе данных PostgreSQL

-- 1. Добавляем новые поля в таблицу contacts
ALTER TABLE public.contacts
ADD COLUMN IF NOT EXISTS postal_code VARCHAR(20),
ADD COLUMN IF NOT EXISTS region VARCHAR(200),
ADD COLUMN IF NOT EXISTS city VARCHAR(200);

-- 2. Добавляем поле registration_address в таблицу passport
ALTER TABLE public.passport
ADD COLUMN IF NOT EXISTS registration_address VARCHAR(1000);

-- 3. Переносим данные адреса регистрации из contacts в passport
-- Сначала обновляем passport для всех записей, где есть соответствующий person с contacts
UPDATE public.passport p
SET registration_address = c.registration_address
FROM public.contacts c
INNER JOIN public.person per ON per.contacts_id = c.id
WHERE p.person_id = per.id
  AND c.registration_address IS NOT NULL
  AND c.registration_address != '';

-- 4. Удаляем поле registration_address из таблицы contacts
-- ВНИМАНИЕ: Убедитесь, что данные перенесены перед выполнением этой команды!
ALTER TABLE public.contacts
DROP COLUMN IF EXISTS registration_address;

-- Комментарии к новым полям
COMMENT ON COLUMN public.contacts.postal_code IS 'Почтовый индекс';
COMMENT ON COLUMN public.contacts.region IS 'Область';
COMMENT ON COLUMN public.contacts.city IS 'Город';
COMMENT ON COLUMN public.contacts.residence_address IS 'Адрес проживания';
COMMENT ON COLUMN public.passport.registration_address IS 'Адрес регистрации';

