using System.Security.Cryptography.X509Certificates;
using DocumentFormat.OpenXml.Office2013.Drawing.ChartStyle;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using SQCScanner.Modal;
using SQCScanner.Modal.CustomTemplate;
using SQCScanner.Modal.Wallet;
using SQCScanner.Services;
using Version1.Modal;
using YourProject.Models;
using static SQCScanner.Modal.CustomTemplate.TempDesignClass;

namespace Version1.Data
{
    public class ApplicationDbContext:DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext>options) :base(options) {  }
        public DbSet<ImgTemp> ImgTemplate { get; set; }
        public DbSet<EmpModel> empModels { get; set; }
        public DbSet<userAuthChecked> LoginTeken { get; set; }
        
        public DbSet<WalletClass> Wallet { get; set; }
        public DbSet<Transaction> Transaction { get; set; }
        public DbSet<Packages> Packages { get; set; }
        public DbSet<PackageList> PackageList { get; set; }
        public DbSet<QrLoginSession> QrLoginSessions { get; set; }
        public DbSet<StaticImgClass> StaticImgClass { get; set; }
        public DbSet<StaticImgDesign> staticImgDesigns { get; set; }

        // yaha se hum code se master table ko handle kar rahe hai override kr rahe hai, 
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ImgTemp>().HasKey(e => e.Id);              
            modelBuilder.Entity<userAuthChecked>().HasKey(e => e.id);      
            modelBuilder.Entity<EmpModel>().HasKey(e => e.Id);
            modelBuilder.Entity<WalletClass>(ef => { ef.HasKey(e => e.SrId); });
            modelBuilder.Entity<Transaction>(ef => { ef.HasKey(e => e.SrId); });
            modelBuilder.Entity<Packages>(ef => { ef.HasKey(e => e.PackId); });
            modelBuilder.Entity<StaticImgClass>().HasKey(e => e.Id);
            modelBuilder.Entity<StaticImgDesign>().HasKey(e => e.Id);
        }
}
}
