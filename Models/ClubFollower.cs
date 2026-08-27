using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    [Table("club_followers")]
    [PrimaryKey("club_id", "user_id")]
    [Index("user_id", Name = "idx_club_followers_user")]
    public partial class ClubFollower
    {
        [Key]
        public ulong club_id { get; set; }

        [Key]
        public ulong user_id { get; set; }

        [MaxLength(6)]
        public DateTime followed_at { get; set; } = DateTime.UtcNow;

        [ForeignKey("club_id")]
        public virtual Club club { get; set; } = null!;

        [ForeignKey("user_id")]
        public virtual User user { get; set; } = null!;
    }
}
