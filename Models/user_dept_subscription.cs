using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("department_id", Name = "idx_user_dept_sub_department")]
[Index("user_id", Name = "idx_user_dept_sub_user")]
[Index("user_id", "department_id", Name = "uq_user_dept_subscription", IsUnique = true)]
public partial class user_dept_subscription
{
    [Key]
    public ulong sub_id { get; set; }

    public ulong user_id { get; set; }

    public ulong department_id { get; set; }

    public bool notify_on_new_event { get; set; } = true;

    [MaxLength(6)]
    public DateTime subscribed_at { get; set; }

    [ForeignKey("department_id")]
    [InverseProperty("user_dept_subscriptions")]
    public virtual Department department { get; set; } = null!;

    [ForeignKey("user_id")]
    [InverseProperty("user_dept_subscriptions")]
    public virtual User user { get; set; } = null!;
}

