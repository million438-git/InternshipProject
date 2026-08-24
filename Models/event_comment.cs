using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("event_id", Name = "idx_event_comments_event")]
[Index("parent_comment_id", Name = "idx_event_comments_parent")]
[Index("user_id", Name = "idx_event_comments_user")]
public partial class event_comment
{
    [Key]
    public ulong id { get; set; }

    public ulong event_id { get; set; }

    public ulong user_id { get; set; }

    public ulong? parent_comment_id { get; set; }

    [Column(TypeName = "text")]
    public string comment { get; set; } = null!;

    public bool is_edited { get; set; }

    [MaxLength(6)]
    public DateTime? edited_at { get; set; }

    public bool is_deleted { get; set; }

    [MaxLength(6)]
    public DateTime? deleted_at { get; set; }

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [MaxLength(6)]
    public DateTime updated_at { get; set; }

    [InverseProperty("parent_comment")]
    public virtual ICollection<event_comment> Inverseparent_comment { get; set; } = new List<event_comment>();

    [ForeignKey("event_id")]
    [InverseProperty("event_comments")]
    public virtual _event _event { get; set; } = null!;

    [ForeignKey("parent_comment_id")]
    [InverseProperty("Inverseparent_comment")]
    public virtual event_comment? parent_comment { get; set; }

    [ForeignKey("user_id")]
    [InverseProperty("event_comments")]
    public virtual User user { get; set; } = null!;
}
