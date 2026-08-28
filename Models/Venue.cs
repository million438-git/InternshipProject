using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("status", Name = "idx_venues_status")]
[Index("venue_type", Name = "idx_venues_type")]
[Index("name", Name = "uq_venues_name", IsUnique = true)]
public partial class Venue
{
    [Key]
    public ulong id { get; set; }

    [StringLength(200)]
    public string name { get; set; } = null!;

    [StringLength(200)]
    public string? building_name { get; set; }

    [StringLength(100)]
    public string? room_number { get; set; }

    [Column(TypeName = "text")]
    public string? description { get; set; }

    public uint capacity { get; set; }

    [Column(TypeName = "enum('CLASSROOM','LECTURE_HALL','AUDITORIUM','LAB','SPORTS_FIELD','MEETING_ROOM','OUTDOOR','OTHER')")]
    public string venue_type { get; set; } = null!;

    [Precision(10, 7)]
    public decimal? latitude { get; set; }

    [Precision(10, 7)]
    public decimal? longitude { get; set; }

    [Column(TypeName = "text")]
    public string? amenities { get; set; }

    [Column(TypeName = "enum('AVAILABLE','MAINTENANCE','INACTIVE')")]
    public string status { get; set; } = null!;

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [MaxLength(6)]
    public DateTime updated_at { get; set; }

    [InverseProperty("venue")]
    public virtual ICollection<_event> _events { get; set; } = new List<_event>();
}
