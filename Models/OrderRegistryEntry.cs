using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Contract2512.Models
{
    [Table("order_registry_entry", Schema = "public")]
    public class OrderRegistryEntry
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("order_number")]
        [Required]
        [MaxLength(100)]
        public string OrderNumber { get; set; } = string.Empty;

        [Column("order_date")]
        [Required]
        public DateTime OrderDate { get; set; } = DateTime.Today;

        [Column("order_subject")]
        [Required]
        [MaxLength(255)]
        public string OrderSubject { get; set; } = string.Empty;

        [Column("listener_name")]
        [Required]
        [MaxLength(500)]
        public string ListenerName { get; set; } = string.Empty;

        [Column("program_name")]
        [Required]
        [MaxLength(500)]
        public string ProgramName { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
