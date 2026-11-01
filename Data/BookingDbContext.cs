using Microsoft.EntityFrameworkCore;
using ServiceBooking.Models;

namespace ServiceBooking.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(
        DbContextOptions<BookingDbContext> options)
        : base(options)
    {
    }

    public DbSet<ServiceItem> Services => Set<ServiceItem>();

    public DbSet<Booking> Bookings => Set<Booking>();
}