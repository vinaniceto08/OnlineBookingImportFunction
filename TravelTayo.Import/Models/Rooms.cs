using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TravelTayo.Import.Models;

namespace HotelbedsAPI.Models
{
    public class Rooms
    {
        [Key]
        public int Id { get; set; } // New surrogate primary key

        public string RoomCode { get; set; } // can now have duplicates per hotel

        public int HotelId { get; set; }
        public Hotel Hotel { get; set; }

        public bool? IsParentRoom { get; set; }
        public int? MinPax { get; set; }
        public int? MaxPax { get; set; }
        public int? MaxAdults { get; set; }
        public int? MaxChildren { get; set; }
        public int? MinAdults { get; set; }

        public string? RoomType { get; set; }
        public string? CharacteristicCode { get; set; }

        public List<RoomFacility> RoomFacilities { get; set; } = new List<RoomFacility>();
        public List<RoomStay> RoomStays { get; set; } = new List<RoomStay>();
    }

    public class RoomFacility
    {
        [Key]
        public int Id { get; set; }

        public int RoomId { get; set; } // FK to Rooms
        public Rooms Room { get; set; }

        public int? FacilityCode { get; set; }
        public int? FacilityGroupCode { get; set; }
        public int? Number { get; set; }
        public bool? IndYesOrNo { get; set; }
        public bool? Voucher { get; set; }
    }

    public class RoomStay
    {
        [Key]
        public int Id { get; set; }

        public int RoomId { get; set; } // FK to Rooms
        public Rooms Room { get; set; }

        public string? StayType { get; set; } // e.g., "BED"
        public int? Order { get; set; } // sequence/order
        public string? Description { get; set; }
    }

    public class RoomType
    {
        [Key]
        public int Id { get; set; }
        public string? Code { get; set; }

        public List<Rooms> Rooms { get; set; } = new List<Rooms>();
    }
}
