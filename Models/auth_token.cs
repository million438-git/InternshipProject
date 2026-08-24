using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("expires_at", Name = "idx_auth_tokens_expires")]
[Index("token_type", Name = "idx_auth_tokens_type")]
[Index("user_id", Name = "idx_auth_tokens_user")]
[Index("token_hash", Name = "uq_auth_tokens_hash", IsUnique = true)]
public partial class auth_token
{
    [Key]
    public ulong id { get; set; }

    public ulong user_id { get; set; }

    public string token_hash { get; set; } = null!;

    [Column(TypeName = "enum('PASSWORD_RESET','EMAIL_VERIFICATION')")]
    public string token_type { get; set; } = null!;

    [MaxLength(6)]
    public DateTime expires_at { get; set; }

    [MaxLength(6)]
    public DateTime? used_at { get; set; }

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [ForeignKey("user_id")]
    [InverseProperty("auth_tokens")]
    public virtual User user { get; set; } = null!;
}
