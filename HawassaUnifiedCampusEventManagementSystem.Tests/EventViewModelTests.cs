using System;
using System.Collections.Generic;
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
        public void CampusMapViewModel_CalculatesTotalsAccurately()
        {
            // Arrange
            var vm = new CampusMapViewModel
            {
                ActiveCampus = "IOT",
                TotalVenuesCount = 12,
                AvailableVenuesCount = 10,
                TotalCapacityCount = 3500
            };

            // Assert
            Assert.Equal("IOT", vm.ActiveCampus);
            Assert.Equal(12, vm.TotalVenuesCount);
            Assert.Equal(10, vm.AvailableVenuesCount);
            Assert.Equal(3500, vm.TotalCapacityCount);
        }

        [Fact]
        public void CheckInViewModel_CalculatesAttendancePercentageAccurately()
        {
            // Arrange
            var vm = new CheckInViewModel
            {
                TotalRegisteredCount = 100,
                AttendedCount = 75
            };

            // Assert
            Assert.Equal(25, vm.PendingCount);
            Assert.Equal(75.0, vm.AttendancePercentage);
        }

        [Fact]
        public void CertificateViewModel_Initialization_RetainsFormattedAttributes()
        {
            // Arrange
            var cert = new CertificateViewModel
            {
                RegistrationId = 42,
                CertificateNumber = "HU-CERT-2026-000042",
                StudentFullName = "Dawit Yohannes",
                StudentIdNumber = "UGR/1234/15",
                EventTitle = "Hawassa AI & Machine Learning Symposium",
                SecurityHash = "A1B2C3D4E5F67890"
            };

            // Assert
            Assert.Equal(42ul, cert.RegistrationId);
            Assert.Equal("HU-CERT-2026-000042", cert.CertificateNumber);
            Assert.Equal("Dawit Yohannes", cert.StudentFullName);
            Assert.Equal("A1B2C3D4E5F67890", cert.SecurityHash);
        }
    }
}
