using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Contract2512.Models
{
    [Table("order_document", Schema = "public")]
    public class OrderDocument
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("order_type_key")]
        [Required]
        [MaxLength(100)]
        public string OrderTypeKey { get; set; } = string.Empty;

        [Column("order_name")]
        [Required]
        [MaxLength(255)]
        public string OrderName { get; set; } = string.Empty;

        [Column("program_id")]
        public long? ProgramId { get; set; }

        [ForeignKey(nameof(ProgramId))]
        public virtual LearningProgram? Program { get; set; }

        [Column("contract_id")]
        public long? ContractId { get; set; }

        [ForeignKey(nameof(ContractId))]
        public virtual Contract? Contract { get; set; }

        [Column("listener_id")]
        public long? ListenerId { get; set; }

        [ForeignKey(nameof(ListenerId))]
        public virtual Person? Listener { get; set; }

        [Column("teacher_id")]
        public long? TeacherId { get; set; }

        [ForeignKey(nameof(TeacherId))]
        public virtual Teacher? Teacher { get; set; }

        [Column("document_number")]
        [MaxLength(100)]
        public string? DocumentNumber { get; set; }

        [Column("file_name")]
        [Required]
        [MaxLength(500)]
        public string FileName { get; set; } = string.Empty;

        [Column("file_path")]
        [Required]
        [MaxLength(1000)]
        public string FilePath { get; set; } = string.Empty;

        [Column("metadata_json")]
        public string? MetadataJson { get; set; }

        [Column("generated_at")]
        [Required]
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        [Column("created_at")]
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
