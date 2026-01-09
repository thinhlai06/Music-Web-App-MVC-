using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MusicWeb.Data;
using MusicWeb.Models.Entities;
using MusicWeb.Services;

namespace MusicWeb;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 6;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // Configure OAuth Authentication
        builder.Services.AddAuthentication()
            .AddGoogle(options =>
            {
                options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
                options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
                options.CallbackPath = "/signin-google";
            })
            .AddFacebook(options =>
            {
                options.AppId = builder.Configuration["Authentication:Facebook:AppId"]!;
                options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"]!;
                options.CallbackPath = "/signin-facebook";
            });

        builder.Services.AddScoped<IMusicService, MusicService>();
        builder.Services.AddScoped<IEmailService, EmailService>();
        
        // Register Cloudflare R2 (S3)
        builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var accessKey = config["CloudflareR2:AccessKey"];
            var secretKey = config["CloudflareR2:SecretKey"];
            var serviceUrl = config["CloudflareR2:ServiceUrl"];

            var s3Config = new Amazon.S3.AmazonS3Config
            {
                ServiceURL = serviceUrl,
            };

            return new Amazon.S3.AmazonS3Client(accessKey, secretKey, s3Config);
        });

        builder.Services.AddScoped<IStorageService, CloudflareStorageService>();
        builder.Services.AddScoped<IUserAlbumService, UserAlbumService>();
        
        // Premium feature services
        builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
        builder.Services.AddScoped<IWalletService, WalletService>();
        builder.Services.AddScoped<IRevenueService, RevenueService>();
        
        // AI Playlist feature
        builder.Services.AddScoped<IAIPlaylistService, AIPlaylistService>();
        
        // Listening Stats feature
        builder.Services.AddScoped<IListeningStatsService, ListeningStatsService>();


        builder.Services.AddHttpClient();
        builder.Services.AddControllersWithViews();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        await DbSeeder.SeedAsync(app.Services);
        await app.RunAsync();
    }
}
