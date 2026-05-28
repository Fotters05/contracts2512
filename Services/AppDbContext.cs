using Microsoft.EntityFrameworkCore;
using Contract2512.Models;

namespace Contract2512.Services
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() { }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Gender> Genders { get; set; }
        public DbSet<Person> Persons { get; set; }
        public DbSet<Passport> Passports { get; set; }
        public DbSet<Contacts> Contacts { get; set; }
        public DbSet<BaseEducation> BaseEducations { get; set; }
        public DbSet<EducationLevel> EducationLevels { get; set; }
        public DbSet<Education> Educations { get; set; }
        public DbSet<ProgramView> ProgramViews { get; set; }
        public DbSet<ContractType> ContractTypes { get; set; }
        public DbSet<LearningProgram> LearningPrograms { get; set; }
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<ProgramModule> ProgramModules { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<WorkloadBatch> WorkloadBatches { get; set; }
        public DbSet<WorkloadDocument> WorkloadDocuments { get; set; }
        public DbSet<WorkloadScheduleEntry> WorkloadScheduleEntries { get; set; }
        public DbSet<HolidayCalendarDay> HolidayCalendarDays { get; set; }
        public DbSet<OrderTemplate> OrderTemplates { get; set; }
        public DbSet<OrderDocument> OrderDocuments { get; set; }
        public DbSet<ListenerApplication> ListenerApplications { get; set; }
        public DbSet<OrderRegistryEntry> OrderRegistryEntries { get; set; }
        public DbSet<TimeOption> TimeOptions { get; set; }
        public DbSet<StudyOption> StudyOptions { get; set; }

        public void EnsureSchemaCompatibility()
        {
            Database.ExecuteSqlRaw(
                """
                ALTER TABLE IF EXISTS public.person
                ALTER COLUMN snils DROP NOT NULL;

                ALTER TABLE IF EXISTS public.person
                ALTER COLUMN snils TYPE VARCHAR(14);

                ALTER TABLE IF EXISTS public.person
                ADD COLUMN IF NOT EXISTS is_archived BOOLEAN NOT NULL DEFAULT FALSE;

                ALTER TABLE IF EXISTS public.person
                ADD COLUMN IF NOT EXISTS archived_at TIMESTAMP NULL;

                ALTER TABLE IF EXISTS public.contract
                ADD COLUMN IF NOT EXISTS is_archived BOOLEAN NOT NULL DEFAULT FALSE;

                ALTER TABLE IF EXISTS public.contract
                ADD COLUMN IF NOT EXISTS archived_at TIMESTAMP NULL;

                ALTER TABLE IF EXISTS public.person
                DROP CONSTRAINT IF EXISTS snils_format;

                ALTER TABLE IF EXISTS public.person
                ADD CONSTRAINT snils_format
                CHECK (
                    snils IS NULL
                    OR snils = ''
                    OR snils ~ '^[0-9]{11}$'
                    OR snils ~ '^[0-9]{3}-[0-9]{3}-[0-9]{3} [0-9]{2}$'
                );

                SELECT setval(pg_get_serial_sequence('public.contacts', 'id'), COALESCE((SELECT MAX(id) FROM public.contacts), 0) + 1, false)
                WHERE pg_get_serial_sequence('public.contacts', 'id') IS NOT NULL;

                SELECT setval(pg_get_serial_sequence('public.person', 'id'), COALESCE((SELECT MAX(id) FROM public.person), 0) + 1, false)
                WHERE pg_get_serial_sequence('public.person', 'id') IS NOT NULL;

                SELECT setval(pg_get_serial_sequence('public.passport', 'id'), COALESCE((SELECT MAX(id) FROM public.passport), 0) + 1, false)
                WHERE pg_get_serial_sequence('public.passport', 'id') IS NOT NULL;

                SELECT setval(pg_get_serial_sequence('public.education', 'id'), COALESCE((SELECT MAX(id) FROM public.education), 0) + 1, false)
                WHERE pg_get_serial_sequence('public.education', 'id') IS NOT NULL;

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

                CREATE TABLE IF NOT EXISTS public.order_registry_entry (
                    id BIGSERIAL PRIMARY KEY,
                    order_number VARCHAR(100) NOT NULL,
                    order_date DATE NOT NULL,
                    order_subject VARCHAR(255) NOT NULL,
                    listener_name VARCHAR(500) NOT NULL,
                    program_name VARCHAR(500) NOT NULL,
                    created_at TIMESTAMP DEFAULT NOW() NOT NULL,
                    updated_at TIMESTAMP DEFAULT NOW() NOT NULL
                );

                CREATE TABLE IF NOT EXISTS public.time_option (
                    id SERIAL PRIMARY KEY,
                    contract_category VARCHAR(20) NOT NULL,
                    name VARCHAR(255) NOT NULL,
                    option_key VARCHAR(100) NOT NULL,
                    text TEXT NOT NULL,
                    hours_per_week INTEGER,
                    weeks_duration INTEGER,
                    sort_order INTEGER NOT NULL DEFAULT 0,
                    is_active BOOLEAN NOT NULL DEFAULT TRUE,
                    created_at TIMESTAMP DEFAULT NOW() NOT NULL,
                    updated_at TIMESTAMP DEFAULT NOW() NOT NULL
                );

                CREATE TABLE IF NOT EXISTS public.study_option (
                    id SERIAL PRIMARY KEY,
                    name VARCHAR(255) NOT NULL,
                    option_key VARCHAR(100) NOT NULL UNIQUE,
                    text TEXT NOT NULL,
                    hours_per_week INTEGER,
                    weeks_duration NUMERIC(6, 2),
                    sort_order INTEGER NOT NULL DEFAULT 0,
                    is_active BOOLEAN NOT NULL DEFAULT TRUE,
                    created_at TIMESTAMP DEFAULT NOW() NOT NULL,
                    updated_at TIMESTAMP DEFAULT NOW() NOT NULL
                );

                ALTER TABLE public.study_option ADD COLUMN IF NOT EXISTS name VARCHAR(255) NOT NULL DEFAULT '';
                ALTER TABLE public.study_option ADD COLUMN IF NOT EXISTS option_key VARCHAR(100) NOT NULL DEFAULT '';
                ALTER TABLE public.study_option ADD COLUMN IF NOT EXISTS text TEXT NOT NULL DEFAULT '';
                ALTER TABLE public.study_option ADD COLUMN IF NOT EXISTS hours_per_week INTEGER;
                ALTER TABLE public.study_option ADD COLUMN IF NOT EXISTS weeks_duration NUMERIC(6, 2);
                ALTER TABLE public.study_option ADD COLUMN IF NOT EXISTS sort_order INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE public.study_option ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT TRUE;
                ALTER TABLE public.study_option ADD COLUMN IF NOT EXISTS created_at TIMESTAMP DEFAULT NOW() NOT NULL;
                ALTER TABLE public.study_option ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP DEFAULT NOW() NOT NULL;

                CREATE INDEX IF NOT EXISTS idx_order_document_program ON public.order_document(program_id);
                CREATE INDEX IF NOT EXISTS idx_person_is_archived ON public.person(is_archived);
                CREATE INDEX IF NOT EXISTS idx_contract_is_archived ON public.contract(is_archived);
                CREATE INDEX IF NOT EXISTS idx_order_registry_entry_order_date ON public.order_registry_entry(order_date);
                CREATE INDEX IF NOT EXISTS idx_order_registry_entry_order_number ON public.order_registry_entry(order_number);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_time_option_category_key ON public.time_option(contract_category, option_key);
                CREATE INDEX IF NOT EXISTS idx_time_option_category ON public.time_option(contract_category);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_study_option_option_key ON public.study_option(option_key);
                CREATE INDEX IF NOT EXISTS idx_study_option_sort_order ON public.study_option(sort_order);
                CREATE INDEX IF NOT EXISTS idx_order_document_listener ON public.order_document(listener_id);
                CREATE INDEX IF NOT EXISTS idx_order_document_teacher ON public.order_document(teacher_id);
                CREATE INDEX IF NOT EXISTS idx_order_document_type ON public.order_document(order_type_key);
                CREATE INDEX IF NOT EXISTS idx_listener_application_listener ON public.listener_application(listener_id);
                CREATE INDEX IF NOT EXISTS idx_listener_application_program ON public.listener_application(program_id);
                CREATE INDEX IF NOT EXISTS idx_listener_application_order_document ON public.listener_application(order_document_id);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_listener_application_contract_type
                    ON public.listener_application(contract_id, application_type_key);

                INSERT INTO public.order_template (order_type_key, name, file_path)
                VALUES
                    ('admission', 'О зачислении', 'C:\Dogovora\Приказы\Пример приказа о зачислении.docx'),
                    ('admission_group', 'О зачислении группа', 'C:\Dogovora\Приказы\Пример приказа о зачислении группа.docx'),
                    ('expulsion', 'Об отчислении', 'C:\Dogovora\Приказы\Пример приказа об отчислении.docx'),
                    ('commission_composition', 'На состав комиссии', 'C:\Dogovora\Приказы\Пример приказа на состав комиссии.docx'),
                    ('final_attestation_admission', 'О допуске итоговой аттестации', 'C:\Dogovora\Приказы\Пример приказа о допуске итоговой аттестации.docx'),
                    ('commission_meeting_protocol', 'О заседании комиссии', 'C:\Dogovora\Приказы\Пример протокола заседании комиссии.docx'),
                    ('statement', 'Ведомости', 'C:\Dogovora\Приказы\Пример ведомости.docx')
                ON CONFLICT (order_type_key) DO NOTHING;
                """);

            SeedDefaultTimeOptions();
            SeedDefaultStudyOptions();
        }

        private void SeedDefaultTimeOptions()
        {
            Database.ExecuteSqlRaw(
                """
                INSERT INTO public.time_option
                    (contract_category, name, option_key, text, hours_per_week, weeks_duration, sort_order, is_active)
                SELECT *
                FROM (VALUES
                    ('PK', 'Опция № 1: 3 часа/нед, 21 неделя', 'Option_Time1', 'Недельная учебная нагрузка по настоящему договору составляет 3 академических часа в неделю, включая 2 академических часа взаимодействия с преподавателем и 1 академический час самостоятельной работы; общая продолжительность освоения — 21 неделя.', 3, 21, 1, TRUE),
                    ('PK', 'Опция № 2: 6 часов/нед, 11 недель', 'Option_Time2', 'Недельная учебная нагрузка по настоящему договору составляет 6 академических часов в неделю, включая 4 академических часа взаимодействия с преподавателем и 2 академических часа самостоятельной работы; общая продолжительность освоения — 11 недель.', 6, 11, 2, TRUE),
                    ('PK', 'Опция № 3: 12 часов/нед, 6 недель', 'Option_Time3', 'Недельная учебная нагрузка по настоящему договору составляет 12 академических часов в неделю, включая 8 академических часов взаимодействия с преподавателем и 4 академических часа самостоятельной работы; общая продолжительность освоения — 6 недель.', 12, 6, 3, TRUE),
                    ('PK', 'Опция № 4: 15 часов/нед, 5 недель', 'Option_Time4', 'Недельная учебная нагрузка по настоящему договору составляет 15 академических часов в неделю, включая 10 академических часов взаимодействия с преподавателем и 5 академических часов самостоятельной работы; общая продолжительность освоения — 5 недель.', 15, 5, 4, TRUE),
                    ('PK', 'Опция № 5: 30 часов/нед, 3 недели', 'Option_Time5', 'Недельная учебная нагрузка по настоящему договору составляет 30 академических часов в неделю, включая 20 академических часов взаимодействия с преподавателем и 10 академических часов самостоятельной работы; общая продолжительность освоения — 3 недели.', 30, 3, 5, TRUE),
                    ('PK', 'Опция № 6: 32 часа/нед, 3 недели', 'Option_Time6', 'Недельная учебная нагрузка по настоящему договору составляет 32 академических часа в неделю, включая 20 академических часов взаимодействия с преподавателем и 12 академических часов самостоятельной работы; общая продолжительность освоения — 3 недели.', 32, 3, 6, TRUE),

                    ('PP', 'Опция № 1: 3 часа/нед, 81 неделя', 'Option_Time1', 'Недельная учебная нагрузка по настоящему договору составляет 3 академических часа в неделю, включая 2 академических часа взаимодействия с преподавателем и 1 академический час самостоятельной работы; общая продолжительность освоения — 81 неделя.', 3, 81, 1, TRUE),
                    ('PP', 'Опция № 2: 6 часов/нед, 41 неделя', 'Option_Time2', 'Недельная учебная нагрузка по настоящему договору составляет 6 академических часов в неделю, включая 4 академических часа взаимодействия с преподавателем и 2 академических часа самостоятельной работы; общая продолжительность освоения — 41 неделя.', 6, 41, 2, TRUE),
                    ('PP', 'Опция № 3: 12 часов/нед, 21 неделя', 'Option_Time3', 'Недельная учебная нагрузка по настоящему договору составляет 12 академических часов в неделю, включая 8 академических часов взаимодействия с преподавателем и 4 академических часа самостоятельной работы; общая продолжительность освоения — 21 неделя.', 12, 21, 3, TRUE),
                    ('PP', 'Опция № 4: 15 часов/нед, 17 недель', 'Option_Time4', 'Недельная учебная нагрузка по настоящему договору составляет 15 академических часов в неделю, включая 10 академических часов взаимодействия с преподавателем и 5 академических часов самостоятельной работы; общая продолжительность освоения — 17 недель.', 15, 17, 4, TRUE),
                    ('PP', 'Опция № 5: 30 часов/нед, 9 недель', 'Option_Time5', 'Недельная учебная нагрузка по настоящему договору составляет 30 академических часов в неделю, включая 20 академических часов взаимодействия с преподавателем и 10 академических часов самостоятельной работы; общая продолжительность освоения — 9 недель.', 30, 9, 5, TRUE),
                    ('PP', 'Опция № 6: 32 часа/нед, 8 недель', 'Option_Time6', 'Недельная учебная нагрузка по настоящему договору составляет 32 академических часа в неделю, включая 20 академических часов взаимодействия с преподавателем и 12 академических часов самостоятельной работы; общая продолжительность освоения — 8 недель.', 32, 8, 6, TRUE),

                    ('DOP', 'Вариант1: 1 час/нед', 'Option_Time1', 'Вариант 1 — с пониженной недельной учебной нагрузкой (1 акад. час в неделю).', 1, NULL, 1, TRUE),
                    ('DOP', 'Вариант2: 2 часа/нед', 'Option_Time2', 'Вариант 2 — с умеренной недельной учебной нагрузкой (2 акад. часа в неделю).', 2, NULL, 2, TRUE),
                    ('DOP', 'Вариант3: 4 часа/нед', 'Option_Time3', 'Вариант 3 — со стандартной недельной учебной нагрузкой (4 акад. часа в неделю).', 4, NULL, 3, TRUE),
                    ('DOP', 'Вариант4: 8 часов/нед', 'Option_Time4', 'Вариант 4 — с высокой недельной учебной нагрузкой (8 акад. часа в неделю).', 8, NULL, 4, TRUE),
                    ('DOP', 'Вариант5: 10 часов/нед', 'Option_Time5', 'Вариант 5 — с повышенной недельной учебной нагрузкой (10 акад. часов в неделю).', 10, NULL, 5, TRUE)
                ) AS seed(contract_category, name, option_key, text, hours_per_week, weeks_duration, sort_order, is_active)
                WHERE NOT EXISTS (SELECT 1 FROM public.time_option)
                ON CONFLICT (contract_category, option_key) DO NOTHING;
                """);
        }

        private void SeedDefaultStudyOptions()
        {
            Database.ExecuteSqlRaw(
                """
                INSERT INTO public.study_option
                    (name, option_key, text, hours_per_week, weeks_duration, sort_order, is_active)
                SELECT *
                FROM (VALUES
                    ('Опция № 1: 1 час/нед, 20 недель', 'Option_study1', 'Недельная учебная нагрузка по настоящему договору составляет 1 академический час в неделю; общая продолжительность освоения — 20 недель.', 1, 20.00, 1, TRUE),
                    ('Опция № 2: 2 часа/нед, 10 недель', 'Option_study2', 'Недельная учебная нагрузка по настоящему договору составляет 2 академических часа в неделю; общая продолжительность освоения — 10 недель.', 2, 10.00, 2, TRUE),
                    ('Опция № 3: 4 часа/нед, 5 недель', 'Option_study3', 'Недельная учебная нагрузка по настоящему договору составляет 4 академических часа в неделю; общая продолжительность освоения — 5 недель.', 4, 5.00, 3, TRUE),
                    ('Опция № 4: 8 часов/нед, 2,5 недели', 'Option_study4', 'Недельная учебная нагрузка по настоящему договору составляет 8 академических часов в неделю; общая продолжительность освоения — 2,5 недели.', 8, 2.50, 4, TRUE),
                    ('Опция № 5: 10 часов/нед, 2 недели', 'Option_study5', 'Недельная учебная нагрузка по настоящему договору составляет 10 академических часов в неделю; общая продолжительность освоения — 2 недели.', 10, 2.00, 5, TRUE)
                ) AS seed(name, option_key, text, hours_per_week, weeks_duration, sort_order, is_active)
                WHERE NOT EXISTS (SELECT 1 FROM public.study_option)
                ON CONFLICT (option_key) DO NOTHING;
                """);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = DbConnectionStringProvider.GetConnectionString();
                
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        "Строка подключения к базе данных не настроена. " +
                        "Пожалуйста, настройте подключение через окно настроек."
                    );
                }
                
                // Отключаем преобразование DateTime в UTC
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                
                optionsBuilder.UseNpgsql(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Настройка для PostgreSQL
            modelBuilder.HasDefaultSchema("public");
            
            // Явная конфигурация для Passport - включаем registration_address
            modelBuilder.Entity<Passport>()
                .ToTable("passport", "public")
                .Property(p => p.RegistrationAddress)
                .HasColumnName("registration_address")
                .HasMaxLength(1000)
                .IsRequired(false);
            
            // Настройка связей для Contract - отключаем каскадное удаление
            modelBuilder.Entity<Contract>()
                .HasOne(c => c.ContractType)
                .WithMany()
                .HasForeignKey(c => c.ContractTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            
            modelBuilder.Entity<Contract>()
                .HasOne(c => c.Program)
                .WithMany()
                .HasForeignKey(c => c.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);
            
            modelBuilder.Entity<Contract>()
                .HasOne(c => c.Payer)
                .WithMany()
                .HasForeignKey(c => c.PayerId)
                .OnDelete(DeleteBehavior.Restrict);
            
            modelBuilder.Entity<Contract>()
                .HasOne(c => c.Listener)
                .WithMany()
                .HasForeignKey(c => c.ListenerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WorkloadBatch>()
                .HasOne(w => w.Program)
                .WithMany()
                .HasForeignKey(w => w.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WorkloadBatch>()
                .HasOne(w => w.Teacher)
                .WithMany()
                .HasForeignKey(w => w.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WorkloadDocument>()
                .HasOne(w => w.Program)
                .WithMany()
                .HasForeignKey(w => w.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WorkloadDocument>()
                .HasOne(w => w.Batch)
                .WithMany()
                .HasForeignKey(w => w.BatchId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WorkloadDocument>()
                .HasOne(w => w.Contract)
                .WithMany()
                .HasForeignKey(w => w.ContractId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WorkloadDocument>()
                .HasOne(w => w.Listener)
                .WithMany()
                .HasForeignKey(w => w.ListenerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WorkloadDocument>()
                .HasOne(w => w.Teacher)
                .WithMany()
                .HasForeignKey(w => w.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WorkloadScheduleEntry>()
                .HasOne(s => s.WorkloadDocument)
                .WithMany()
                .HasForeignKey(s => s.WorkloadDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WorkloadBatch>()
                .HasIndex(w => w.ProgramId);

            modelBuilder.Entity<WorkloadBatch>()
                .HasIndex(w => w.TeacherId);

            modelBuilder.Entity<WorkloadDocument>()
                .HasIndex(w => w.ProgramId);

            modelBuilder.Entity<WorkloadDocument>()
                .HasIndex(w => w.TeacherId);

            modelBuilder.Entity<WorkloadDocument>()
                .HasIndex(w => w.BatchId);

            modelBuilder.Entity<WorkloadDocument>()
                .HasIndex(w => w.ContractId);

            modelBuilder.Entity<WorkloadDocument>()
                .HasIndex(w => w.ListenerId);

            modelBuilder.Entity<WorkloadScheduleEntry>()
                .HasIndex(s => s.WorkloadDocumentId);

            modelBuilder.Entity<HolidayCalendarDay>()
                .HasIndex(h => h.HolidayDate)
                .IsUnique();

            modelBuilder.Entity<OrderTemplate>()
                .HasIndex(t => t.OrderTypeKey)
                .IsUnique();

            modelBuilder.Entity<OrderDocument>()
                .HasOne(d => d.Program)
                .WithMany()
                .HasForeignKey(d => d.ProgramId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<OrderDocument>()
                .HasOne(d => d.Contract)
                .WithMany()
                .HasForeignKey(d => d.ContractId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<OrderDocument>()
                .HasOne(d => d.Listener)
                .WithMany()
                .HasForeignKey(d => d.ListenerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<OrderDocument>()
                .HasOne(d => d.Teacher)
                .WithMany()
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<OrderDocument>()
                .HasIndex(d => d.OrderTypeKey);

            modelBuilder.Entity<OrderDocument>()
                .HasIndex(d => d.ProgramId);

            modelBuilder.Entity<OrderDocument>()
                .HasIndex(d => d.ListenerId);

            modelBuilder.Entity<OrderDocument>()
                .HasIndex(d => d.TeacherId);

            modelBuilder.Entity<OrderRegistryEntry>()
                .HasIndex(e => e.OrderDate);

            modelBuilder.Entity<OrderRegistryEntry>()
                .HasIndex(e => e.OrderNumber);

            modelBuilder.Entity<TimeOption>()
                .HasIndex(o => new { o.ContractCategory, o.OptionKey })
                .IsUnique();

            modelBuilder.Entity<TimeOption>()
                .HasIndex(o => o.ContractCategory);

            modelBuilder.Entity<StudyOption>()
                .HasIndex(o => o.OptionKey)
                .IsUnique();

            modelBuilder.Entity<StudyOption>()
                .HasIndex(o => o.SortOrder);

            modelBuilder.Entity<ListenerApplication>()
                .HasOne(a => a.Contract)
                .WithMany()
                .HasForeignKey(a => a.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ListenerApplication>()
                .HasOne(a => a.Listener)
                .WithMany()
                .HasForeignKey(a => a.ListenerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ListenerApplication>()
                .HasOne(a => a.Program)
                .WithMany()
                .HasForeignKey(a => a.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ListenerApplication>()
                .HasOne(a => a.OrderDocument)
                .WithMany()
                .HasForeignKey(a => a.OrderDocumentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ListenerApplication>()
                .HasIndex(a => a.ListenerId);

            modelBuilder.Entity<ListenerApplication>()
                .HasIndex(a => a.ProgramId);

            modelBuilder.Entity<ListenerApplication>()
                .HasIndex(a => a.OrderDocumentId);

            modelBuilder.Entity<ListenerApplication>()
                .HasIndex(a => new { a.ContractId, a.ApplicationTypeKey })
                .IsUnique();
        }
    }
}
