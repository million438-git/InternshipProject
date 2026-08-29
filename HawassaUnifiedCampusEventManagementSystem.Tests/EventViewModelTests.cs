using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HawassaUnifiedCampusEventManagementSystem.Models;
using Xunit;

namespace HawassaUnifiedCampusEventManagementSystem.Tests
{
    public class EventViewModelTests
    {
        [Fact]
        public void EventViewModel_DefaultInitialization_ShouldHaveEmptyCollections()
        {
            // Act
            var ev = new Event();

            // Assert
            Assert.NotNull(ev.Comments);
            Assert.Empty(ev.Comments);
            Assert.NotNull(ev.Feedbacks);
            Assert.Empty(ev.Feedbacks);
            Assert.Equal(0, ev.AverageRating);
            Assert.Equal(0, ev.TotalRatings);
        }

        [Fact]
        public void EventFeedbackItemViewModel_PropertiesAssignment_ShouldRetainValues()
        {
            // Arrange
            var feedback = new EventFeedbackItemViewModel
            {
                Id = 101,
                UserName = "Abebe Kebede",
                Rating = 5,
                Comment = "Outstanding campus tech symposium!",
                IsAnonymous = false,
                CreatedAt = DateTime.UtcNow
            };

            // Assert
            Assert.Equal(101ul, feedback.Id);
            Assert.Equal("Abebe Kebede", feedback.UserName);
            Assert.Equal(5, feedback.Rating);
            Assert.Equal("Outstanding campus tech symposium!", feedback.Comment);
            Assert.False(feedback.IsAnonymous);
        }

        [Fact]
        public void EventCommentItemViewModel_PropertiesAssignment_ShouldRetainValues()
        {
            // Arrange
            var comment = new EventCommentItemViewModel
            {
                Id = 202,
                UserId = 55,
                UserName = "Hiwot Tadesse",
                CommentText = "Will certificates be provided to attendees?",
                CreatedAt = DateTime.UtcNow,
                CanDelete = true
            };

            // Assert
            Assert.Equal(202ul, comment.Id);
            Assert.Equal(55ul, comment.UserId);
            Assert.Equal("Hiwot Tadesse", comment.UserName);
            Assert.True(comment.CanDelete);
        }

        [Fact]
        public void EventViewModel_WithoutTitle_ShouldFailValidation()
        {
            var ev = new Event
            {
                Category = "Academic",
                EventDate = DateTime.Today.AddDays(3),
                StartTime = TimeSpan.FromHours(9)
            };
            var context = new ValidationContext(ev);
            var results = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(ev, context, results, validateAllProperties: true);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(Event.Title)));
        }

        [Fact]
        public void EventViewModel_WithRequiredFields_ShouldPassValidation()
        {
            var ev = new Event
            {
                Title = "Campus Symposium",
                Category = "Academic",
                EventDate = DateTime.Today.AddDays(3),
                StartTime = TimeSpan.FromHours(9)
            };
            var context = new ValidationContext(ev);
            var results = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(ev, context, results, validateAllProperties: true);

            Assert.True(isValid);
        }
    }
}
