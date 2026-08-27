using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    [Table("club_members")]
    [Index("user_id", Name = "idx_club_members_user")]
    [Index("club_id", Name = "idx_club_members_club")]
    [Index("club_id", "user_id", Name = "uq_club_user_member", IsUnique = true)]
    public partial class ClubMember
    {
        [Key]
        public ulong id { get; set; }

        public ulong club_id { get; set; }

        public ulong user_id { get; set; }

        [Column(TypeName = "enum('MEMBER','OFFICER','SECRETARY','TREASURER','PRESIDENT','ADMIN')")]
        public string membership_role { get; set; } = "MEMBER";

        [Column(TypeName = "enum('PENDING','APPROVED','REJECTED')")]
        public string status { get; set; } = "PENDING";

        [Column(TypeName = "text")]
        public string? request_notes { get; set; }

        [MaxLength(6)]
        public DateTime applied_at { get; set; } = DateTime.UtcNow;

        [MaxLength(6)]
        public DateTime? reviewed_at { get; set; }

        public ulong? reviewed_by { get; set; }

        [ForeignKey("club_id")]
        public virtual Club club { get; set; } = null!;

        [ForeignKey("user_id")]
        public virtual User user { get; set; } = null!;

        [ForeignKey("reviewed_by")]
        public virtual User? reviewer { get; set; }
    }
}
