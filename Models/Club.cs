using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    [Table("clubs")]
    [Index("slug", Name = "uq_clubs_slug", IsUnique = true)]
    [Index("faculty_id", Name = "idx_clubs_faculty")]
    [Index("department_id", Name = "idx_clubs_dept")]
    [Index("organization_id", Name = "idx_clubs_org")]
    [Index("president_id", Name = "idx_clubs_president")]
    public partial class Club
    {
        [Key]
        public ulong id { get; set; }

        [StringLength(200)]
        [Required]
        public string name { get; set; } = null!;

        [StringLength(220)]
        [Required]
        public string slug { get; set; } = null!;

        [StringLength(100)]
        public string? short_name { get; set; }

        [Column(TypeName = "text")]
        public string? description { get; set; }

        [StringLength(1000)]
        public string? logo_url { get; set; }

        [StringLength(1000)]
        public string? cover_image_url { get; set; }

        public ulong? faculty_id { get; set; }
        public ulong? department_id { get; set; }
        public ulong? organization_id { get; set; }
        public ulong? president_id { get; set; }

        [Column(TypeName = "enum('ACTIVE','PENDING','SUSPENDED','INACTIVE')")]
        public string status { get; set; } = "ACTIVE";

        [MaxLength(6)]
        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [MaxLength(6)]
        public DateTime updated_at { get; set; } = DateTime.UtcNow;

        // Relationships
        [ForeignKey("faculty_id")]
        public virtual Faculty? faculty { get; set; }

        [ForeignKey("department_id")]
        public virtual Department? department { get; set; }

        [ForeignKey("organization_id")]
        public virtual Organization? organization { get; set; }

        [ForeignKey("president_id")]
        public virtual User? president { get; set; }

        public virtual ICollection<ClubInterest> club_interests { get; set; } = new List<ClubInterest>();
        public virtual ICollection<ClubFollower> club_followers { get; set; } = new List<ClubFollower>();
        public virtual ICollection<ClubMember> club_members { get; set; } = new List<ClubMember>();
    }
}
