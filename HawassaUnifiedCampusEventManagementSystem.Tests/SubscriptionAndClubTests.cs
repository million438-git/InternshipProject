using System;
using HawassaUnifiedCampusEventManagementSystem.Models;
using Xunit;

namespace HawassaUnifiedCampusEventManagementSystem.Tests
{
    public class SubscriptionAndClubTests
    {
        [Fact]
        public void UserDeptSubscription_DefaultNotification_IsTrue()
        {
            var sub = new user_dept_subscription
            {
                user_id = 101,
                department_id = 5,
                subscribed_at = DateTime.UtcNow
            };

            Assert.True(sub.notify_on_new_event);
        }

        [Fact]
        public void UserDeptSubscription_ExplicitFalse_IsPreserved()
        {
            var sub = new user_dept_subscription
            {
                user_id = 102,
                department_id = 8,
                notify_on_new_event = false,
                subscribed_at = DateTime.UtcNow
            };

            Assert.False(sub.notify_on_new_event);
        }

        [Fact]
        public void Club_DefaultStatusAndCollections_AreProperlyInitialized()
        {
            var club = new Club
            {
                name = "Robotics & AI Guild",
                slug = "robotics-ai-guild"
            };

            Assert.Equal("ACTIVE", club.status);
            Assert.NotNull(club.club_interests);
            Assert.NotNull(club.club_followers);
            Assert.NotNull(club.club_members);
            Assert.Empty(club.club_interests);
            Assert.Empty(club.club_followers);
            Assert.Empty(club.club_members);
        }

        [Fact]
        public void ClubMember_DefaultRoleAndStatus_AreCorrect()
        {
            var member = new ClubMember
            {
                club_id = 1,
                user_id = 55
            };

            Assert.Equal("MEMBER", member.membership_role);
            Assert.Equal("PENDING", member.status);
            Assert.Null(member.reviewed_at);
            Assert.Null(member.reviewed_by);
        }

        [Fact]
        public void EventCategory_PropertiesAndSlug_AreAssignedCorrectly()
        {
            var category = new event_category
            {
                name = "Academic Symposium",
                slug = "academic-symposium",
                is_active = true
            };

            Assert.True(category.is_active);
            Assert.Equal("academic-symposium", category.slug);
            Assert.Equal("Academic Symposium", category.name);
            Assert.NotNull(category._events);
        }
    }
}
