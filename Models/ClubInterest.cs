using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    [Table("club_interests")]
    [PrimaryKey("club_id", "category_id")]
    [Index("category_id", Name = "idx_club_interests_category")]
    public partial class ClubInterest
    {
        [Key]
        public ulong club_id { get; set; }

        [Key]
        public ulong category_id { get; set; }

        [MaxLength(6)]
        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [ForeignKey("club_id")]
        public virtual Club club { get; set; } = null!;

        [ForeignKey("category_id")]
        public virtual event_category category { get; set; } = null!;
    }
}
