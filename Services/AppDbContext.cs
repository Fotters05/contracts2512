using System.Data.Entity;
using Contract2512.Models;

namespace Contract2512.Services
{
    [DbConfigurationType(typeof(NpgsqlDbConfiguration))]
    public class AppDbContext : DbContext
    {
        public AppDbContext() : base("name=DefaultConnection") { }

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

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Настройка для PostgreSQL enum типа
            modelBuilder.HasDefaultSchema("public");
        }
    }
}
