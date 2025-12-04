using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Contract2512.Models
{
    [Table("education", Schema = "public")]
    public class Education
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("person_id")]
        [Required]
        public long PersonId { get; set; }

        [ForeignKey("PersonId")]
        public virtual Person Person { get; set; }

        [Column("enrollment_date")]
        [Required]
        public DateTime EnrollmentDate { get; set; }

        [Column("base_education_id")]
        public short? BaseEducationId { get; set; }

        [ForeignKey("BaseEducationId")]
        public virtual BaseEducation BaseEducation { get; set; }

        [Column("education_level_id")]
        public short? EducationLevelId { get; set; }

        [ForeignKey("EducationLevelId")]
        public virtual EducationLevel EducationLevel { get; set; }

        [Column("series")]
        [MaxLength(50)]
        public string Series { get; set; }

        [Column("number")]
        [Required]
        [MaxLength(100)]
        public string Number { get; set; }

        [Column("issue_date")]
        [Required]
        public DateTime IssueDate { get; set; }

        [Column("issued_by")]
        [Required]
        [MaxLength(500)]
        public string IssuedBy { get; set; }

        [Column("place_of_issue")]
        [Required]
        [MaxLength(500)]
        public string PlaceOfIssue { get; set; }

        [Column("specialty")]
        [MaxLength(300)]
        public string Specialty { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}

