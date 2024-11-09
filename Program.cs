using DownloaderApp.API.Services;
using YoutubeExplode;

var builder = WebApplication.CreateBuilder(args);

// Configuração do cliente YoutubeClient
builder.Services.AddSingleton<YoutubeClient>();

// Configuração de CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowProductionOrigin", policy =>
    {
        // Permite apenas a origem específica da produção
        policy.WithOrigins("https://downloaderappapi-production.up.railway.app") 
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Adicionar suporte a controllers e Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuração do HttpClient para injeção de dependência
builder.Services.AddHttpClient();

// Registrar o serviço de download
builder.Services.AddScoped<DownloaderService>();

var app = builder.Build();

// Configuração do Swagger apenas em desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Downloader API V1");
    });
}

// Adiciona o middleware CORS para produção
app.UseCors("AllowProductionOrigin");

// Middleware para HTTPS e autorização
app.UseHttpsRedirection();
app.UseAuthorization();

// Mapear os controllers
app.MapControllers();

// Executar a aplicação
app.Run();
