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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Строка подключения к PostgreSQL
                var connectionString = "Host=localhost;Port=5432;Username=postgres;Password=1;Database=MPT2512";
                
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
        }
    }
}
