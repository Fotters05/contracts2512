-- Объединенный скрипт миграций для базы данных
-- Выполните этот скрипт в вашей базе данных PostgreSQL
-- ВНИМАНИЕ: Выполняйте скрипты по порядку!

-- ============================================
-- 0. Миграция для изменения обязательных полей таблицы person на опциональные
-- ============================================
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

-- ============================================
-- 0.5. Миграция для изменения обязательных полей таблицы passport на опциональные
-- ============================================
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

-- ============================================
-- 0.6. Миграция для изменения обязательных полей таблицы education на опциональные
-- ============================================
ALTER TABLE public.education
ALTER COLUMN enrollment_date DROP NOT NULL;

-- Комментарии к полям
COMMENT ON COLUMN public.education.enrollment_date IS 'Дата зачисления (опционально)';

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

