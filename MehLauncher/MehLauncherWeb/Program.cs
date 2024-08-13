var builder = WebApplication.CreateBuilder(args);

// ������������ ����������
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// ���������� �������� � ���������
builder.Services.AddControllers();
builder.Services.AddLogging(); 

var app = builder.Build();

// ������������ ��������� ��������� HTTP-��������
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Urls.Add("http://0.0.0.0:5024");

app.Run();