using System;
using System.Collections.Generic;

namespace TravelTayo.Import.Models;

public class Hotel
{
    public Hotel()
    {
        Boards = new List<Board>();
        Segments = new List<Segment>();
        Phones = new List<HotelPhone>();
        Rooms = new List<Room>();
        Facilities = new List<Facility>();
        Terminals = new List<Terminal>();
        Images = new List<Image>();
        Wildcards = new List<HotelWildcard>();
    }

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int CountryId { get; set; }
    public Country? Country { get; set; }

    public int StateId { get; set; }
    public State? State { get; set; }

    public int ZoneId { get; set; }
    public Zone? Zone { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int CategoryGroupId { get; set; }
    public CategoryGroup? CategoryGroup { get; set; }

    public int AccommodationTypeId { get; set; }
    public AccommodationType? AccommodationType { get; set; }

    public List<Board> Boards { get; set; }
    public List<Segment> Segments { get; set; }

    public int AddressId { get; set; }
    public Address? Address { get; set; }
    public string? Email { get; set; }
    public string? License { get; set; }
    public int? GiataCode { get; set; }

    public List<HotelPhone> Phones { get; set; }
    public List<Room> Rooms { get; set; }
    public List<Facility> Facilities { get; set; }
    public List<Terminal> Terminals { get; set; }
    public List<Image> Images { get; set; }
    public List<HotelWildcard> Wildcards { get; set; }

    public string? Web { get; set; }
    public DateTime LastUpdate { get; set; }
    public string? S2C { get; set; }
    public int Ranking { get; set; }
}

public class Country { public int Id { get; set; } public string? Code { get; set; } public string? IsoCode { get; set; } public string? Description { get; set; } public List<Hotel>? Hotels { get; set; } }
public class State { public int Id { get; set; } public string? Code { get; set; } public string? Name { get; set; } public List<Hotel>? Hotels { get; set; } }
public class Zone { public int Id { get; set; } public int ZoneCode { get; set; } public string? Name { get; set; } public string? Description { get; set; } public List<Hotel>? Hotels { get; set; } }
public class Category { public int Id { get; set; } public string? Code { get; set; } public string? Description { get; set; } public List<Hotel>? Hotels { get; set; } }
public class CategoryGroup { public int Id { get; set; } public string? Code { get; set; } public string? Description { get; set; } public List<Hotel>? Hotels { get; set; } }
public class AccommodationType { public int Id { get; set; } public string? Code { get; set; }  public List<Hotel>? Hotels { get; set; } }
public class Board { public int Id { get; set; } public string? Code { get; set; } public string? Description { get; set; } }
public class Segment { public int Id { get; set; } public int Code { get; set; } public string? Description { get; set; } }
public class Address { public int Id { get; set; } public string? Content { get; set; } public string? Street { get; set; } public string? Number { get; set; } public string? PostalCode { get; set; } public string? City { get; set; } public List<Hotel>? Hotels { get; set; } }
public class Room { 
    public int Id { get; set; } 
    public string? roomCode { get; set; } 

}
public class Facility { public int Id { get; set; } public string? Name { get; set; } }
public class Terminal { public int Id { get; set; } public string? TerminalCode { get; set; } }
public class Image { public int Id { get; set; } public string? Path { get; set; } }
public class HotelPhone { public int Id { get; set; } public int HotelId { get; set; } public Hotel? Hotel { get; set; } public string? Phone { get; set; } }
public class HotelWildcard { public int Id { get; set; } public int HotelId { get; set; } public Hotel? Hotel { get; set; } public string? Value { get; set; } }
