using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CINEMA.Models
{
    public class FaceEmbedding
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public int AdminId { get; set; }

        [Required]
        public string FaceId { get; set; } // persistedFaceId from Azure Face API

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property (optional)
        [ForeignKey("AdminId")]
        public Admin Admin { get; set; }
    }
}
