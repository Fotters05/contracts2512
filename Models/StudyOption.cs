using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Contract2512.Models
{
    [Table("study_option", Schema = "public")]
    public class StudyOption
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("name")]
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("option_key")]
        [Required]
        [MaxLength(100)]
        public string OptionKey { get; set; } = string.Empty; // "Option_study1", "Option_study2", etc.

        [Column("text")]
        [Required]
        public string Text { get; set; } = string.Empty;

        [Column("hours_per_week")]
        public int? HoursPerWeek { get; set; }

        [Column("weeks_duration")]
        public decimal? WeeksDuration { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

