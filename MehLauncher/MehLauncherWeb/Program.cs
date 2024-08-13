var builder = WebApplication.CreateBuilder(args);

// Конфигурация приложения
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Добавление сервисов в контейнер
builder.Services.AddControllers();
builder.Services.AddLogging(); 

var app = builder.Build();

// Конфигурация конвейера обработки HTTP-запросов
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Urls.Add("http://0.0.0.0:5024");

app.Run();