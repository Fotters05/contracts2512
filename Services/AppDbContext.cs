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

        public void EnsureSchemaCompatibility()
        {
            Database.ExecuteSqlRaw(
                """
                ALTER TABLE IF EXISTS public.person
                ALTER COLUMN snils DROP NOT NULL;

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

                CREATE INDEX IF NOT EXISTS idx_order_document_program ON public.order_document(program_id);
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
