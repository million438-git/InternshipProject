using System;
using HawassaUnifiedCampusEventManagementSystem.Models;
using Xunit;

namespace HawassaUnifiedCampusEventManagementSystem.Tests
{
    public class AdminUserApprovalTests
    {
        [Fact]
        public void AdminUserApprovalViewModel_PaginationCalculations_AreAccurate()
        {
            var vm = new AdminUserApprovalViewModel
            {
                PageSize = 20,
                TotalFilteredCount = 55,
                CurrentPage = 2
            };

            Assert.Equal(3, vm.TotalPages);
            Assert.True(vm.HasPreviousPage);
            Assert.True(vm.HasNextPage);
        }

        [Fact]
        public void AdminUserApprovalViewModel_SinglePageBoundary_CalculatesCorrectly()
        {
            var vm = new AdminUserApprovalViewModel
            {
                PageSize = 20,
                TotalFilteredCount = 15,
                CurrentPage = 1
            };

            Assert.Equal(1, vm.TotalPages);
            Assert.False(vm.HasPreviousPage);
            Assert.False(vm.HasNextPage);
        }

        [Fact]
        public void AdminUserApprovalItem_TimeWaitingFormatted_ReturnsSensibleDurations()
        {
            var recentItem = new AdminUserApprovalItem
            {
                RegisteredAt = DateTime.UtcNow.AddMinutes(-15)
            };

            var hoursAgoItem = new AdminUserApprovalItem
            {
                RegisteredAt = DateTime.UtcNow.AddHours(-5)
            };

            var daysAgoItem = new AdminUserApprovalItem
            {
                RegisteredAt = DateTime.UtcNow.AddDays(-3)
            };

            Assert.Contains("waiting", recentItem.TimeWaitingFormatted);
            Assert.Contains("5h waiting", hoursAgoItem.TimeWaitingFormatted);
            Assert.Contains("3d waiting", daysAgoItem.TimeWaitingFormatted);
        }

        [Fact]
        public void AdminUsersViewModel_PaginationCalculations_AreAccurate()
        {
            var vm = new AdminUsersViewModel
            {
                PageSize = 25,
                TotalFilteredCount = 105,
                CurrentPage = 3
            };

            Assert.Equal(5, vm.TotalPages);
            Assert.True(vm.HasPreviousPage);
            Assert.True(vm.HasNextPage);
        }

        [Fact]
        public void AdminEventsViewModel_PaginationAndCalculations_AreAccurate()
        {
            var vm = new AdminEventsViewModel
            {
                PageSize = 10,
                TotalFilteredCount = 42,
                CurrentPage = 2
            };

            Assert.Equal(5, vm.TotalPages);
            Assert.True(vm.HasPreviousPage);
            Assert.True(vm.HasNextPage);
        }

        [Fact]
        public void AdminEventRow_TimeStatusAndCapacity_CalculatesAccurately()
        {
            var upcomingEvt = new AdminEventRow
            {
                StartAt = DateTime.UtcNow.AddDays(4),
                EndAt = DateTime.UtcNow.AddDays(4).AddHours(2),
                Capacity = 100,
                RegistrationCount = 75
            };

            Assert.Contains("In 4d", upcomingEvt.TimeStatus);
            Assert.Equal(75, upcomingEvt.CapacityPercentage);

            var pastEvt = new AdminEventRow
            {
                StartAt = DateTime.UtcNow.AddDays(-2),
                EndAt = DateTime.UtcNow.AddDays(-1),
                Capacity = 50,
                RegistrationCount = 50
            };

            Assert.Equal("Past", pastEvt.TimeStatus);
            Assert.Equal(100, pastEvt.CapacityPercentage);
        }

        [Fact]
        public void AdminOrganizationsViewModel_PaginationCalculations_AreAccurate()
        {
            var vm = new AdminOrganizationsViewModel
            {
                PageSize = 15,
                TotalFilteredCount = 48,
                CurrentPage = 2
            };

            Assert.Equal(4, vm.TotalPages);
            Assert.True(vm.HasPreviousPage);
            Assert.True(vm.HasNextPage);
        }

        [Fact]
        public void AdminFacultiesAndDepartmentsViewModels_PaginationCalculations_AreAccurate()
        {
            var facVm = new AdminFacultiesViewModel
            {
                PageSize = 10,
                TotalFilteredCount = 28,
                CurrentPage = 1
            };

            Assert.Equal(3, facVm.TotalPages);
            Assert.False(facVm.HasPreviousPage);
            Assert.True(facVm.HasNextPage);

            var deptVm = new AdminDepartmentsViewModel
            {
                PageSize = 20,
                TotalFilteredCount = 65,
                CurrentPage = 4
            };

            Assert.Equal(4, deptVm.TotalPages);
            Assert.True(deptVm.HasPreviousPage);
            Assert.False(deptVm.HasNextPage);
        }

        [Fact]
        public void AdminVenuesViewModel_PaginationAndTierCalculations_AreAccurate()
        {
            var vm = new AdminVenuesViewModel
            {
                PageSize = 10,
                TotalFilteredCount = 25,
                CurrentPage = 2
            };

            Assert.Equal(3, vm.TotalPages);
            Assert.True(vm.HasPreviousPage);
            Assert.True(vm.HasNextPage);

            var megaVenue = new AdminVenueRow { Capacity = 1500 };
            var largeHall = new AdminVenueRow { Capacity = 450 };
            var smallRoom = new AdminVenueRow { Capacity = 30 };

            Assert.Equal("Mega Venue", megaVenue.CapacityTier);
            Assert.Equal("Large Hall", largeHall.CapacityTier);
            Assert.Equal("Intimate / Seminar", smallRoom.CapacityTier);
        }
    }
}
