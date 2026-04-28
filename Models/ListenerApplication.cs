using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Contract2512.Models
{
    [Table("listener_application", Schema = "public")]
    public class ListenerApplication
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("application_type_key")]
        [Required]
        [MaxLength(100)]
        public string ApplicationTypeKey { get; set; } = string.Empty;

        [Column("contract_id")]
        [Required]
        public long ContractId { get; set; }

        [ForeignKey(nameof(ContractId))]
        public virtual Contract? Contract { get; set; }

        [Column("listener_id")]
        [Required]
        public long ListenerId { get; set; }

        [ForeignKey(nameof(ListenerId))]
        public virtual Person? Listener { get; set; }

        [Column("program_id")]
        [Required]
        public long ProgramId { get; set; }

        [ForeignKey(nameof(ProgramId))]
        public virtual LearningProgram? Program { get; set; }

        [Column("order_document_id")]
        public long? OrderDocumentId { get; set; }

        [ForeignKey(nameof(OrderDocumentId))]
        public virtual OrderDocument? OrderDocument { get; set; }

        [Column("application_number")]
        [MaxLength(100)]
        public string? ApplicationNumber { get; set; }

        [Column("application_date")]
        [Required]
        public DateTime ApplicationDate { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
