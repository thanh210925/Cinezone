using System;

namespace CINEMA.Models;

public class Popup
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public string? ButtonLink { get; set; }

    public bool IsActive { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}