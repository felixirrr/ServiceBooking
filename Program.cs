using Microsoft.EntityFrameworkCore;
using ServiceBooking.Data;
using ServiceBooking.Models;

var builder = WebApplication.CreateBuilder(args);

// Подключение контроллеров и Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Регистрация базы данных SQLite
builder.Services.AddDbContext<BookingDbContext>(options =>
{
    options.UseSqlite("Data Source=booking.db");
});

// Разрешаем обращения к API с веб-страницы
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Включение Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Подключение CORS
app.UseCors("AllowFrontend");

// Раздача файлов из папки wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Создание базы данных и добавление начальных услуг
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<BookingDbContext>();

    db.Database.EnsureCreated();

    if (!db.Services.Any())
    {
        db.Services.AddRange(
            new ServiceItem
            {
                Name = "Стрижка",
                Description = "Стрижка в салоне",
                DurationMinutes = 60,
                Price = 1500,
                IsActive = true
            },
            new ServiceItem
            {
                Name = "Консультация",
                Description = "Индивидуальная консультация",
                DurationMinutes = 30,
                Price = 1000,
                IsActive = true
            },
            new ServiceItem
            {
                Name = "Массаж",
                Description = "Расслабляющий массаж",
                DurationMinutes = 60,
                Price = 2000,
                IsActive = true
            }
        );

        db.SaveChanges();
    }
}

// Проверочный маршрут
app.MapGet("/api", () =>
{
    return Results.Ok(new
    {
        message = "API системы бронирования работает"
    });
});

// Получение всех активных услуг
app.MapGet("/api/services", async (BookingDbContext db) =>
{
    var services = await db.Services
        .Where(service => service.IsActive)
        .OrderBy(service => service.Name)
        .ToListAsync();

    return Results.Ok(services);
});

// Получение одной услуги по идентификатору
app.MapGet("/api/services/{id:int}", async (
    int id,
    BookingDbContext db) =>
{
    var service = await db.Services
        .FirstOrDefaultAsync(service => service.Id == id);

    if (service == null)
    {
        return Results.NotFound(new
        {
            message = "Услуга не найдена"
        });
    }

    return Results.Ok(service);
});

// Добавление новой услуги
app.MapPost("/api/services", async (
    ServiceItem service,
    BookingDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(service.Name))
    {
        return Results.BadRequest(new
        {
            message = "Название услуги обязательно"
        });
    }

    if (service.DurationMinutes <= 0)
    {
        return Results.BadRequest(new
        {
            message = "Длительность должна быть больше нуля"
        });
    }

    if (service.Price < 0)
    {
        return Results.BadRequest(new
        {
            message = "Цена не может быть отрицательной"
        });
    }

    service.Id = 0;
    service.Name = service.Name.Trim();
    service.Description = service.Description?.Trim() ?? "";
    service.IsActive = true;

    db.Services.Add(service);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/services/{service.Id}",
        service);
});

// Получение всех бронирований
app.MapGet("/api/bookings", async (BookingDbContext db) =>
{
    var bookings = await db.Bookings
        .Include(booking => booking.ServiceItem)
        .OrderBy(booking => booking.StartTime)
        .ToListAsync();

    return Results.Ok(bookings);
});

// Получение бронирования по идентификатору
app.MapGet("/api/bookings/{id:int}", async (
    int id,
    BookingDbContext db) =>
{
    var booking = await db.Bookings
        .Include(booking => booking.ServiceItem)
        .FirstOrDefaultAsync(booking => booking.Id == id);

    if (booking == null)
    {
        return Results.NotFound(new
        {
            message = "Бронирование не найдено"
        });
    }

    return Results.Ok(booking);
});

// Создание бронирования
app.MapPost("/api/bookings", async (
    CreateBookingRequest request,
    BookingDbContext db) =>
{
    // Проверка имени клиента
    if (string.IsNullOrWhiteSpace(request.CustomerName))
    {
        return Results.BadRequest(new
        {
            message = "Введите имя клиента"
        });
    }

    // Проверка email
    if (string.IsNullOrWhiteSpace(request.CustomerEmail))
    {
        return Results.BadRequest(new
        {
            message = "Введите электронную почту"
        });
    }

    // Поиск выбранной услуги
    var service = await db.Services
        .FirstOrDefaultAsync(service =>
            service.Id == request.ServiceItemId &&
            service.IsActive);

    if (service == null)
    {
        return Results.NotFound(new
        {
            message = "Выбранная услуга не найдена"
        });
    }

    // Проверка, что время находится в будущем
    if (request.StartTime <= DateTime.Now)
    {
        return Results.BadRequest(new
        {
            message = "Нельзя забронировать прошедшее время"
        });
    }

    // Время окончания новой услуги
    DateTime requestedEndTime =
        request.StartTime.AddMinutes(service.DurationMinutes);

    // Получаем активные бронирования
    var activeBookings = await db.Bookings
        .Where(booking =>
            booking.Status == "Active" &&
            booking.StartTime.Date == request.StartTime.Date)
        .Include(booking => booking.ServiceItem)
        .ToListAsync();

    // Проверяем пересечение временных интервалов
    bool timeIsBusy = activeBookings.Any(booking =>
    {
        if (booking.ServiceItem == null)
        {
            return false;
        }

        DateTime existingStartTime =
            booking.StartTime;

        DateTime existingEndTime =
            booking.StartTime.AddMinutes(
                booking.ServiceItem.DurationMinutes);

        bool intervalsIntersect =
            request.StartTime < existingEndTime &&
            requestedEndTime > existingStartTime;

        return intervalsIntersect;
    });

    if (timeIsBusy)
    {
        return Results.Conflict(new
        {
            message = "Выбранное время уже занято"
        });
    }

    // Создаём новое бронирование
    var newBooking = new Booking
    {
        CustomerName = request.CustomerName.Trim(),
        CustomerEmail = request.CustomerEmail.Trim(),
        ServiceItemId = request.ServiceItemId,
        StartTime = request.StartTime,
        Status = "Active"
    };

    db.Bookings.Add(newBooking);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/bookings/{newBooking.Id}",
        newBooking);
});

// Отмена бронирования
app.MapDelete("/api/bookings/{id:int}", async (
    int id,
    BookingDbContext db) =>
{
    var booking = await db.Bookings
        .FirstOrDefaultAsync(booking => booking.Id == id);

    if (booking == null)
    {
        return Results.NotFound(new
        {
            message = "Бронирование не найдено"
        });
    }

    if (booking.Status == "Cancelled")
    {
        return Results.BadRequest(new
        {
            message = "Это бронирование уже отменено"
        });
    }

    booking.Status = "Cancelled";

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = "Бронирование отменено"
    });
});

// Запуск приложения
app.Run();