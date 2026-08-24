using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Table("event_feedback")]
[Index("event_id", Name = "idx_event_feedback_event")]
[Index("user_id", Name = "idx_event_feedback_user")]
[Index("event_id", "user_id", Name = "uq_event_feedback_user", IsUnique = true)]
public partial class event_feedback
{
    [Key]
    public ulong id { get; set; }

    public ulong event_id { get; set; }

    public ulong user_id { get; set; }

    public byte rating { get; set; }

    [Column(TypeName = "text")]
    public string? comment { get; set; }

    public bool is_anonymous { get; set; }

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [MaxLength(6)]
    public DateTime updated_at { get; set; }

    [ForeignKey("event_id")]
    [InverseProperty("event_feedbacks")]
    public virtual _event _event { get; set; } = null!;

    [ForeignKey("user_id")]
    [InverseProperty("event_feedbacks")]
    public virtual User user { get; set; } = null!;
}
