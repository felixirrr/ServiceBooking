namespace ServiceBooking.Models;

public class ServiceItem
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public int DurationMinutes { get; set; } = 60;

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
}

public class Booking
{
    public int Id { get; set; }

    public string CustomerName { get; set; } = "";

    public string CustomerEmail { get; set; } = "";

    public int ServiceItemId { get; set; }

    public DateTime StartTime { get; set; }

    public string Status { get; set; } = "Active";

    // Навигационное свойство: услуга, связанная с бронированием.
    public ServiceItem? ServiceItem { get; set; }
}

public class CreateBookingRequest
{
    public string CustomerName { get; set; } = "";

    public string CustomerEmail { get; set; } = "";

    public int ServiceItemId { get; set; }

    public DateTime StartTime { get; set; }
}