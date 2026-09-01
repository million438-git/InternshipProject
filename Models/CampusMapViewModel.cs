using System;
using System.Collections.Generic;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    public class CampusMapViewModel
    {
        public string ActiveCampus { get; set; } = "MAIN";
        public List<CampusInfo> Campuses { get; set; } = new();
        public List<VenueMapItem> Venues { get; set; } = new();
        public int TotalVenuesCount { get; set; }
        public int AvailableVenuesCount { get; set; }
        public int TotalCapacityCount { get; set; }
    }

    public class CampusInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";
        public string Description { get; set; } = "";
        public string LocationTag { get; set; } = "";
        public int VenueCount { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public int DefaultZoom { get; set; } = 16;
    }

    public class VenueMapItem
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = "";
        public string Campus { get; set; } = "MAIN";
        public string CampusName { get; set; } = "Main Campus";
        public string? BuildingName { get; set; }
        public string? RoomNumber { get; set; }
        public int Capacity { get; set; }
        public string? VenueType { get; set; }
        public string Status { get; set; } = "AVAILABLE";
        public string? Description { get; set; }
        public string? Amenities { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string GoogleMapsDirectionUrl => $"https://www.google.com/maps/dir/?api=1&destination={Lat:F6},{Lng:F6}";
        public List<string> EquipmentList { get; set; } = new();
        public List<UpcomingVenueEvent> UpcomingEvents { get; set; } = new();
    }

    public class UpcomingVenueEvent
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = "";
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public string CategoryName { get; set; } = "";
        public string OrganizerName { get; set; } = "";
    }
}
