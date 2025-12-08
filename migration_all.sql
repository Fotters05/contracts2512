-- Объединенный скрипт миграций для базы данных
-- Выполните этот скрипт в вашей базе данных PostgreSQL
-- ВНИМАНИЕ: Выполняйте скрипты по порядку!

-- ============================================
-- 1. Миграция для добавления полей опций в таблицу contract
-- ============================================
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

-- ============================================
-- 2. Миграция для обновления таблиц contacts и passport
-- ============================================
-- 2.1. Добавляем новые поля в таблицу contacts
ALTER TABLE public.contacts
ADD COLUMN IF NOT EXISTS postal_code VARCHAR(20),
ADD COLUMN IF NOT EXISTS region VARCHAR(200),
ADD COLUMN IF NOT EXISTS city VARCHAR(200);

-- 2.2. Добавляем поле registration_address в таблицу passport
ALTER TABLE public.passport
ADD COLUMN IF NOT EXISTS registration_address VARCHAR(1000);

-- 2.3. Переносим данные адреса регистрации из contacts в passport
-- Сначала обновляем passport для всех записей, где есть соответствующий person с contacts
UPDATE public.passport p
SET registration_address = c.registration_address
FROM public.contacts c
INNER JOIN public.person per ON per.contacts_id = c.id
WHERE p.person_id = per.id
  AND c.registration_address IS NOT NULL
  AND c.registration_address != '';

-- 2.4. Удаляем поле registration_address из таблицы contacts
-- ВНИМАНИЕ: Убедитесь, что данные перенесены перед выполнением этой команды!
ALTER TABLE public.contacts
DROP COLUMN IF EXISTS registration_address;

-- Комментарии к новым полям
COMMENT ON COLUMN public.contacts.postal_code IS 'Почтовый индекс';
COMMENT ON COLUMN public.contacts.region IS 'Область';
COMMENT ON COLUMN public.contacts.city IS 'Город';
COMMENT ON COLUMN public.contacts.residence_address IS 'Адрес проживания';
COMMENT ON COLUMN public.passport.registration_address IS 'Адрес регистрации';

-- ============================================
-- 3. Миграция для добавления поля city в таблицу education
-- ============================================
ALTER TABLE public.education
ADD COLUMN IF NOT EXISTS city VARCHAR(200);

-- Комментарий к полю
COMMENT ON COLUMN public.education.city IS 'Город получения образования';

