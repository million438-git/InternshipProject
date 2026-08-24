using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("author_id", Name = "idx_announcements_author")]
[Index("department_id", Name = "idx_announcements_department")]
[Index("status", Name = "idx_announcements_status")]
[Index("slug", Name = "uq_announcements_slug", IsUnique = true)]
public partial class Announcement
{
    [Key]
    public ulong id { get; set; }

    [StringLength(255)]
    public string title { get; set; } = null!;

    [StringLength(300)]
    public string slug { get; set; } = null!;

    [Column(TypeName = "text")]
    public string content { get; set; } = null!;

    [StringLength(500)]
    public string? summary { get; set; }

    public ulong? author_id { get; set; }

    public ulong? department_id { get; set; }

    [Column(TypeName = "enum('NEWS','NOTICE','ALERT','CLOSURE','ACADEMIC','CAREER','GENERAL')")]
    public string announcement_type { get; set; } = null!;

    [Column(TypeName = "enum('LOW','NORMAL','HIGH','URGENT')")]
    public string priority { get; set; } = null!;

    [StringLength(1000)]
    public string? image_url { get; set; }

    [MaxLength(6)]
    public DateTime? published_at { get; set; }

    [MaxLength(6)]
    public DateTime? expires_at { get; set; }

    [Column(TypeName = "enum('DRAFT','PUBLISHED','ARCHIVED')")]
    public string status { get; set; } = null!;

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [MaxLength(6)]
    public DateTime updated_at { get; set; }

    [ForeignKey("author_id")]
    [InverseProperty("announcements")]
    public virtual User? author { get; set; }

    [ForeignKey("department_id")]
    [InverseProperty("announcements")]
    public virtual Department? department { get; set; }
}
