using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Contract2512.Models
{
    [Table("learning_program", Schema = "public")]
    public class LearningProgram
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("name")]
        [Required]
        [MaxLength(500)]
        public string Name { get; set; }

        [Column("format")]
        [Required]
        [MaxLength(200)]
        public string Format { get; set; }

        [Column("program_view_id")]
        [Required]
        public int ProgramViewId { get; set; }

        [ForeignKey("ProgramViewId")]
        public virtual ProgramView? ProgramView { get; set; }

        [Column("hours")]
        [Required]
        public int Hours { get; set; }

        [Column("lessons_count")]
        [Required]
        public int LessonsCount { get; set; }

        [Column("price", TypeName = "numeric")]
        [Required]
        public decimal Price { get; set; }

        [Column("image")]
        [MaxLength(255)]
        public string? Image { get; set; }

        [Column("source_url")]
        [MaxLength(500)]
        public string? SourceUrl { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [NotMapped]
        public string DisplayName => $"{Name} ({FormatDisplayName})";

        [NotMapped]
        public string FormatDisplayName
        {
            get
            {
                if (IsGroupFormat)
                {
                    return "в группе";
                }

                if (IsIndividualFormat)
                {
                    return "индивидуально";
                }

                return Format ?? string.Empty;
            }
        }

        [NotMapped]
        public bool IsGroupFormat =>
            !string.IsNullOrWhiteSpace(Format) &&
            Format.Contains("в группе", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsIndividualFormat =>
            !string.IsNullOrWhiteSpace(Format) &&
            Format.Contains("индивидуально", StringComparison.OrdinalIgnoreCase);
    }
}
